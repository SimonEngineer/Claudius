# Weaver

Weaver is a point-and-click web scraping system with a built-in, n8n-style automation/workflow
engine. Build a scraping project by clicking on the page you want to scrape (pick the list, the
fields per item, and the "next page" link), then wire it into a drag-and-drop workflow that
triggers on a schedule, a webhook, or an event -- and can email you, post to Discord, run a C#
script, or log to a file when something changes.

Scraping is rate-limited and queued in a way that's safe to run as multiple independent
instances: the queue and the rate limiter both live in Redis, so instance A hitting a site's
rate limit blocks instance B from hammering the same site too, and a job is only ever handed to
one worker at a time.

## Architecture

```
src/
  Weaver.Domain          entities: ScrapingProject, FieldSelector, RateLimitPolicy, ScrapeRun,
                          ScrapedItem, Workflow, WorkflowNode/Edge, WorkflowRun, NodeRun
  Weaver.Infrastructure   EF Core (Postgres) persistence, Redis token-bucket rate limiter,
                          Redis Streams job queue
  Weaver.Scraping         AngleSharp selector-based scraper + pagination, the page proxy that
                          powers the point-and-click picker UI
  Weaver.Workflows        node registry + execution engine + event bus, and the built-in blocks
                          (triggers, condition, scrape action, email, Discord, code, file logger)
  Weaver.Api              ASP.NET Core Web API: CRUD for projects/workflows, the proxy endpoint,
                          webhook endpoint, manual run endpoints
  Weaver.Worker           background services: Redis stream consumer (scrape jobs), cron
                          scheduler tick
frontend/                 React + TypeScript + Vite: dashboard, scraping project builder
                          (iframe picker), workflow builder (React Flow canvas)
```

### Distributed rate limiting

Each `RateLimitPolicy` defines a permit limit, a window, a burst capacity, and a `KeyScope`
(per host, per exact URL, per scraping project, or a custom template like `{host}{path}`).
At scrape time the policy + target URL resolve to a Redis key, and a single Lua script run
atomically in Redis implements a token bucket against that key using Redis's own clock (so
clock skew between instances doesn't matter). Every process sharing that Redis sees the same
bucket, so the limit is enforced across however many API/worker instances you run.

### Distributed queue

Scrape jobs are pushed onto a Redis stream (`weaver:scrape-jobs`) and consumed via a consumer
group (`weaver-workers`). Every worker process is a distinct consumer in that group: Redis
guarantees a given message is only handed to one consumer at a time. If a worker dies mid-job,
`XAUTOCLAIM` lets another instance reclaim it once its lease goes idle, and a `ScrapeJob` row in
Postgres tracks status/attempts for the UI. Failed jobs retry (via reclaim) up to 3 attempts
before being marked Failed.

### Workflow engine

A `Workflow` is a graph of `WorkflowNode`s and `WorkflowEdge`s (exactly what the React Flow
canvas edits). Running a workflow uses a dataflow scheduler starting from one trigger node: a
node only runs once every incoming edge has either delivered a value or been pruned (its source
ran but took a different branch), so a **merge node with two upstream paths waits for both**
instead of firing on whichever arrives first, combining their outputs into one object keyed by
each predecessor's name. A Condition node's `true`/`false` result decides which edges are "taken"
(so the untaken branch's nodes never run at all), and edges tagged `error` route around a failed
node's default output. A node can be configured with **retries** (`MaxRetries`/`RetryDelayMs`) so
a transient failure (a flaky SMTP send, a temporary 503) doesn't fail the whole run immediately.

Every node's output stays addressable **by name** for the rest of the run, not just by its
immediate successor: `NodeExecutionContext.AllNodeOutputs` (`Nodes` inside a Code Block script)
lets a Condition, template, or script three hops downstream reference `"Scrape Fixture.items"`
just as easily as the node right before it. Everything is `JsonNode` under the hood so nodes
don't need to know about each other's shapes ahead of time.

Nodes are just implementations of `INodeHandler` registered in DI -- adding a new block type is
adding one class. Built-in blocks:

| Type | What it does |
|---|---|
| `trigger.manual` | Fired by the UI's Run button |
| `trigger.cron` | Fired by the Worker's cron scheduler (config: `cronExpression`) |
| `trigger.http` | Fired by `POST /api/webhooks/{workflowId}/{nodeId}` (config: optional `secret`) |
| `trigger.event` | Fired by `IWorkflowEventPublisher.PublishAsync` (config: `eventName`) |
| `condition` | Branches true/false on a field (config: `field`, `operator`, `value`; both accept `NodeName.path` and `{{templates}}`) |
| `action.scrape` | Queues a scraping project run on the same distributed job queue as "Run now" and waits for a worker to finish it (config: `scrapingProjectId`) -- requires at least one `Weaver.Worker` instance running |
| `action.sendEmail` | Sends an SMTP email (config: `to` (comma-separated, templated), `subject`, `body`) |
| `action.sendDiscord` | Posts to a Discord webhook (config: `webhookUrl`, `message`) |
| `action.code` | Runs a C# script via Roslyn scripting, with `Data`/`Nodes`/`Db`/`Log`/`PublishEventAsync` globals and a configurable `timeoutSeconds` |
| `action.fileLogger` | Appends to a csv/ndjson/txt file (config: `filePath`, `format`) |

The scraper raises `scrape.item.found`, `scrape.item.changed`, `scrape.run.completed`, and
`scrape.run.failed` events after every run, so a workflow with an Event Trigger node listening
for `scrape.item.changed` is how you build "email me when the price drops." Change-detection
matches items across runs by whichever field(s) are marked **Key** in the project's field list --
without one, Weaver falls back to page URL + position, so mark a stable field (a detail URL, a
SKU) as Key whenever you want reliable change tracking.

**Using it from code, not just the GUI:** anything with access to `IWorkflowEventPublisher` (a
plain DI service) can call `PublishAsync("my.event", payload)` and have the exact same effect as
a workflow author dragging an Event Trigger block onto the canvas -- no GUI required.

### Point-and-click page picker

`PageProxyService` fetches the target page server-side (respecting the same rate limiter as real
runs), adds a `<base href>` tag so relative links/images still resolve, strips any
CSP/X-Frame-Options the page declares via `<meta>`, and appends a small overlay script before
handing it back. The frontend puts that in an iframe; clicking an element posts its selector back
via `postMessage`. Clicks never navigate the iframe (they're all `preventDefault`ed) -- to browse
elsewhere, change the preview URL in the parent app. When picking a field, the parent tells the
overlay the current "item selector" so it can report a selector *relative to the item container*
(works identically for every row) rather than an absolute one that would only match the single
element you clicked.

Before saving, use **Test extraction** to run the current selectors against the previewed page
and see exactly what they'd produce -- without persisting anything or spending a real (queued)
scrape run.

### Rendering modes

Each project has a `RenderMode`: `Http` (plain GET + parse -- fast, no browser) or `Playwright`
(renders the page in headless Chromium first, so client-side/JS-rendered content is present
before extraction runs). The picker, test-extraction, and real scrape runs all honor this setting
via the same `IPageFetcherFactory`, so a Playwright-mode project previews against the actually-
rendered DOM too. Chromium isn't downloaded by default -- set `WEAVER_ENABLE_PLAYWRIGHT=true` on
an instance to have it install itself at startup (see docker-compose.yml's worker services), or
point `WEAVER_PLAYWRIGHT_EXECUTABLE_PATH` at a Chromium binary you've provisioned yourself.

## Running locally

### Docker Compose (Postgres + Redis + API + 2 workers + frontend)

```
docker compose up --build
```

Frontend: http://localhost:5173, API/Swagger: http://localhost:5080/swagger. Two worker
containers run side by side to demonstrate the distributed queue/rate-limiter -- scale further
with `docker compose up --scale worker-1=3`.

### Manual dev setup

Prerequisites: .NET 8 SDK, Node 20+, Postgres, Redis.

```
# Postgres/Redis running locally on their default ports, then:
dotnet run --project src/Weaver.Api      # applies EF Core migrations on startup
dotnet run --project src/Weaver.Worker   # run one or more of these

cd frontend
npm install
npm run dev                              # http://localhost:5173, expects the API on :5080
```

Connection strings live in `src/Weaver.Api/appsettings.json` / `src/Weaver.Worker/appsettings.json`
under `ConnectionStrings:Postgres` / `ConnectionStrings:Redis`; SMTP settings for the Send Email
block are under `Smtp`.

## Adding a new workflow block

1. Implement `INodeHandler` in `Weaver.Workflows/Nodes/` (`Type` is the registry key, e.g.
   `action.mything`; `ExecuteAsync` reads `context.Config`/`context.Input` and returns a
   `NodeExecutionResult`).
2. Register it in `Weaver.Workflows/ServiceCollectionExtensions.cs`.
3. Add an entry to `frontend/src/components/nodeCatalog.ts` (label, description, default config)
   and, if it needs configuration fields, a branch in `frontend/src/components/NodeConfigPanel.tsx`.
