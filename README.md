# Claudius

A self-hosted orchestrator for running autonomous coding tasks across two LLM tiers:
a cheap/fast **local model** (via [Aider](https://aider.chat)) that does the actual
implementation work, and a more capable **cloud model** (via headless
[Claude Code](https://docs.claude.com/en/docs/claude-code)) that plans the work
up front and verifies it afterwards. Tasks queue across multiple projects at once,
keep moving the moment a human resolves an approval, and stream live token/tool/diff
output to a React dashboard over SignalR.

See [`docs/design/local-llm-task-queue.md`](docs/design/local-llm-task-queue.md) for
the full design rationale.

## How it works

- **Supervisor lane** (Claude Code, cloud model): turns a task description into a
  concrete implementation plan + acceptance criteria (`Plan`), and later reviews the
  worker's diff against those criteria (`Verify`). A plan with multiple independent
  steps is decomposed into child tasks that run in parallel/sequence and bubble back
  up to the parent once all are done.
- **Worker lane** (Aider, local model): implements one step against the project's
  repo, committing its own changes as it goes.
- **Scheduler**: a `PeriodicTimer`-driven tick (not Hangfire cron — too coarse)
  claims runnable tasks with Postgres row locks (`FOR UPDATE SKIP LOCKED`) and
  dispatches them onto one of two Hangfire queues (`supervisor`, `worker`), each with
  its own concurrency cap. Stale leases (crashed workers) are requeued automatically.
- **Approvals**: when a worker is blocked and needs a human decision, the task parks
  in `AwaitingInput` and frees its slot immediately — the queue never stalls on one
  task. Approvals broadcast live to a dashboard inbox via SignalR.
- **Workspace isolation**: each task tree gets its own git worktree/branch, keyed
  off the root task id, so concurrent decomposed plans never share a working tree.
- **Skills**: when verification passes, the supervisor can record a reusable "skill"
  (a short playbook), which gets fed back into future Plan/Verify prompts for that
  project.
- **Cancellation**: any in-flight task can be cancelled from the dashboard; this
  kills the underlying `claude`/`aider` process without touching DB persistence.
- All model calls are routed through a [LiteLLM](https://www.litellm.ai/) gateway so
  cloud and local models share one OpenAI/Anthropic-compatible endpoint, with
  per-provider timeouts (local calls can legitimately take tens of minutes).

## Stack

- **API**: ASP.NET Core 8, EF Core (Npgsql), Hangfire (Postgres storage), SignalR,
  Serilog (structured JSON logs), OpenTelemetry tracing.
- **Database**: PostgreSQL.
- **LLM gateway**: LiteLLM, fronting Anthropic (cloud) and Ollama (local).
- **Engines**: Claude Code CLI (supervisor), Aider CLI (worker) — both invoked as
  subprocesses via `ProcessStartInfo.ArgumentList` (no shell interpolation).
- **Dashboard**: React + TypeScript + Vite + Tailwind + hand-rolled shadcn/ui
  components, `@microsoft/signalr` for live updates.

## Project layout

```
src/
  Orchestrator.Domain/          # entities, enums, engine contracts (no EF/infra deps)
  Orchestrator.Infrastructure/  # EF Core, Hangfire jobs, scheduler, engine adapters
  Orchestrator.Api/             # controllers, SignalR hub, Program.cs wiring
  Orchestrator.Tests/           # xUnit tests for engine adapters
web/                            # React dashboard
docs/design/                    # design doc
docker-compose.yml              # postgres + litellm + api, for running the whole stack
litellm-config.yaml             # LiteLLM model routing/timeouts
```

## Running locally

### Prerequisites

- .NET 8 SDK
- Node 20+
- Docker (for Postgres + LiteLLM), or your own Postgres instance
- [`claude`](https://docs.claude.com/en/docs/claude-code) and
  [`aider`](https://aider.chat/docs/install.html) CLIs installed and on `PATH`
  (the API shells out to both)
- An Anthropic API key, and/or a running [Ollama](https://ollama.com) host for the
  local model

### 1. Start Postgres + LiteLLM

```bash
export ANTHROPIC_API_KEY=sk-ant-...
docker compose up postgres litellm
```

### 2. Run the API

```bash
cd src/Orchestrator.Api
export ConnectionStrings__Orchestrator="Host=localhost;Port=5432;Database=orchestrator;Username=orchestrator;Password=orchestrator"
export ANTHROPIC_BASE_URL="http://localhost:4000"
export AIDER_OPENAI_API_BASE="http://localhost:4000"
dotnet run
```

EF Core migrations run automatically on startup. The API listens on
`http://localhost:5139` by default; Swagger is available at `/swagger` in
Development, and the Hangfire dashboard at `/hangfire` (localhost-only).

### 3. Run the dashboard

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:5173`. Set `VITE_API_BASE_URL` if the API isn't on the
default port.

### Or: everything in Docker

```bash
export ANTHROPIC_API_KEY=sk-ant-...
docker compose up --build
```

This builds the API image (which installs both `claude` and `aider` CLIs), and
mounts `./workspaces` into the container for project repos.

## Tests

```bash
dotnet test src/Orchestrator.sln
cd web && npx tsc -b && npx vite build
```

## Configuration

Key `Scheduler` options (`appsettings.json` or environment, e.g.
`Scheduler__WorkerConcurrency`):

| Option | Default | Meaning |
|---|---|---|
| `SupervisorConcurrency` | 3 | Max concurrent cloud-model runs across all projects |
| `WorkerConcurrency` | 1 | Max concurrent local-model runs across all projects |
| `TickInterval` | 5s | How often the scheduler looks for runnable tasks |
| `LeaseDuration` | 45m | Must exceed the slowest expected local-model call |
| `SupervisorRunTimeout` | 5m | Per-call timeout, cloud model |
| `WorkerRunTimeout` | 30m | Per-call timeout, local model |

Per-project model routing (`localModel` / `cloudModel`, e.g.
`ollama/qwen2.5-coder:32b` / `anthropic/claude-sonnet-4-6`) and worker concurrency
caps are set when creating a project via the API/dashboard.
