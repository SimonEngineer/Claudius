# Multi-Tier Agent Orchestrator — Design Doc & Implementation Plan

## 1. Problem statement

Run an always-on system that develops multiple software projects concurrently, where:

- **Cheap/local models** (via Ollama, LocalAI, etc.) do the bulk of the work: writing code, implementing plans, running commands, iterating on diffs.
- **Flagship models** (Claude Sonnet/Opus, GPT-4-class) act as the "management layer": breaking goals into plans and tasks, choosing/creating skills, scheduling work, reviewing the local model's output, and deciding on follow-up fixes.
- Many projects can be queued at once. Only as many run concurrently as the (slow) local hardware allows; while a task is blocked on local-model compute or on a human approval, the scheduler immediately picks up the next runnable task from any project's queue.
- Everything streams live (token-by-token LLM output + state transitions) to a dashboard, so the system is observable in real time.
- The system must tolerate local models that are slow (minutes per call) and occasionally flaky, without falling over — long timeouts, retries, checkpointing, structured logs/traces throughout.

## 2. Build-vs-integrate decision

Three options were considered:

| Option | Description | Verdict |
|---|---|---|
| A. Clone Claude Code from scratch | Build our own agent loop, tool-use harness, sandboxing, diff application, etc. | Rejected — reinvents a huge amount of mature, hard-to-get-right machinery (permissions, sandboxing, context management, tool-call parsing per model family) for no benefit. |
| B. Wrap/route Claude Code itself for both tiers | Point Claude Code's model config at a local proxy so it talks to Ollama directly. | Rejected as the *sole* engine — Claude Code's tool-calling format is tuned for Claude; small local models (7B–14B-class code models) are unreliable at Claude's native tool-call/XML-ish protocol, causing malformed edits and broken sessions. Also Claude Code is built for one interactive session, not an always-on multi-project daemon. |
| **C. Two-engine split + custom orchestrator** (chosen) | Use **Claude Code** (via the Claude Agent SDK / headless `claude -p`) as the **supervisor engine** for planning, skill/goal creation, scheduling decisions, and verification — it's already excellent at this and we keep using real Anthropic models for it. Use **Aider** (open source, mission-built for exactly "point any OpenAI-compatible/Ollama model at a repo and have it edit code") as the **worker engine** for cheap local-model implementation. Build a new orchestrator daemon on top that owns the task queue, concurrency lanes, streaming, persistence, and observability. | **Chosen.** Reuses two proven, actively maintained agent harnesses; all genuinely new engineering effort goes into the orchestration layer, which is the part that doesn't exist anywhere off the shelf. |

Key enabler: Claude Code supports **headless/programmatic invocation** (`claude -p "<prompt>" --output-format stream-json`, plus the Claude Agent SDK for TypeScript/Python), which gives us scriptable control, streaming JSON events, hooks, and permission modes without needing an interactive TTY. That's exactly the shape our supervisor needs.

Aider was chosen for the worker tier because it: already supports Ollama/LocalAI/any OpenAI-compatible endpoint out of the box, works well in non-interactive/scripted mode (`aider --message "..." --yes`), produces real git commits per change (cheap checkpointing for free), and is far more forgiving of weaker models' tool-calling quirks (it uses a more constrained diff format rather than open-ended tool use).

If at any point a given local model proves capable enough to use Claude Code's own tool-calling protocol reliably, the worker tier can be swapped to "Claude Code pointed at a local model via the LLM gateway" — the orchestrator doesn't care which engine fulfills a worker task, it only cares about the engine adapter interface (§5.3).

## 3. High-level architecture

```
                         ┌───────────────────────────────────────────┐
                         │              Web/CLI Dashboard             │
                         │  live task tree · token stream · approvals │
                         └───────────────▲─────────────────┬─────────┘
                                          │ SSE/WebSocket    │ REST (approve/reject/new task)
                         ┌────────────────┴──────────────────▼────────┐
                         │                Orchestrator API              │
                         │   (HTTP+WS server, auth, project CRUD)       │
                         └───────────────▲──────────────────┬──────────┘
                                          │                  │
                ┌─────────────────────────┴───┐    ┌─────────▼──────────────┐
                │        Scheduler             │    │      Event Bus         │
                │  lane-based, priority, fair   │◄──┤  (in-proc pub/sub +    │
                │  picks next runnable task      │    │   persisted log)      │
                └───────────────┬───────────────┘    └─────────▲──────────────┘
                                │ assigns Run                    │ emits events
                ┌───────────────▼───────────────┐    ┌──────────┴─────────────┐
                │         Worker Pool            │    │   Supervisor Pool       │
                │  N concurrent "local" lanes    │    │  M concurrent "cloud"   │
                │  Engine: Aider + local LLM      │    │  lanes. Engine: Claude  │
                │  (Ollama/LocalAI via gateway)   │    │  Code (Agent SDK)       │
                └───────────────┬───────────────┘    └──────────┬─────────────┘
                                │                                 │
                         ┌──────▼─────────────────────────────────▼──────┐
                         │                LLM Gateway (LiteLLM proxy)      │
                         │  unifies Anthropic / OpenAI / Ollama / LocalAI  │
                         │  per-provider timeouts, retries, rate limits    │
                         └──────┬───────────────────────────────────────┬──┘
                                │                                       │
                       ┌────────▼────────┐                    ┌─────────▼─────────┐
                       │ Local model host  │                    │ Anthropic/OpenAI   │
                       │ Ollama / LocalAI  │                    │ APIs               │
                       └───────────────────┘                    └────────────────────┘

                ┌──────────────────────────────────────────────────────────┐
                │  Persistence: Postgres (state) + object store (logs/artifacts) │
                │  Observability: structured logs, OpenTelemetry traces, metrics  │
                │  Workspaces: one git worktree per project, isolated per run      │
                └──────────────────────────────────────────────────────────┘
```

## 4. Core concepts & data model

**Project** — a repo/workspace the system manages. Has its own git checkout (or worktree), config (which local model, which Anthropic model, repo path/URL, default lanes/priority).

**Goal** — a high-level user-supplied objective for a project ("add OAuth login"). The supervisor decomposes a Goal into one or more **Tasks**.

**Task** — a unit of work with a state machine (below). Belongs to a project, optionally a parent Goal. Has a priority and an assigned lane class (`supervisor` or `worker`).

**Run** — one execution attempt of a Task by an engine (Aider run or Claude Code run). A Task can have multiple Runs (retries, fix-up passes). A Run has a stream of **Events** (tokens, tool calls, file diffs, status changes) and ends with a **Result** (diff produced, summary, exit status).

**Approval** — a Task can pause in `awaiting_input` state with a question/diff for the user; resolving it (approve/reject/answer) is what unblocks the scheduler to resume it.

**Skill** — a reusable prompt/playbook the supervisor can author and store (e.g. "how to write a DB migration in this repo"), attached to a project, injected into future supervisor/worker prompts.

### Task state machine

```
queued → planning (supervisor) → ready_for_work → in_progress (worker)
   → verifying (supervisor) → done
                              ↘ needs_fix → ready_for_work (loop)
                              ↘ awaiting_input → ready_for_work | needs_fix (after resolution)
   any state → failed (after max retries) → dead_letter
```

- `queued`: just created, no plan yet.
- `planning`: supervisor (Claude Code) is turning the Goal/Task description into a concrete implementation plan + acceptance criteria, may also create/update Skills.
- `ready_for_work`: plan exists, eligible to be picked up by a worker lane.
- `in_progress`: worker (Aider + local model) is implementing.
- `verifying`: supervisor reviews the diff/test results against acceptance criteria.
- `needs_fix`: supervisor found issues, writes a follow-up instruction, loops back to `ready_for_work`.
- `awaiting_input`: either engine asked the user something, or a destructive/ambiguous action needs explicit approval.
- `done` / `failed` / `dead_letter`: terminal states.

### Postgres schema (sketch)

```sql
projects(id, name, repo_path, git_remote, local_model, cloud_model, max_worker_concurrency, priority, created_at)
goals(id, project_id, description, status, created_at)
tasks(id, project_id, goal_id, parent_task_id, title, description, lane, state,
      priority, retry_count, plan jsonb, acceptance_criteria jsonb,
      created_at, updated_at)
runs(id, task_id, engine, model, status, started_at, finished_at,
     cost_usd, tokens_in, tokens_out, exit_summary jsonb)
events(id, run_id, ts, type, payload jsonb)          -- token chunks, tool_call, file_diff, log
approvals(id, task_id, run_id, question, options jsonb, status, resolved_by, resolved_at)
skills(id, project_id, name, content, created_by_run_id, created_at)
```

`events` is high-volume (token-level) — write-ahead in batches, and prune/roll up old raw token events after a run finishes while keeping the final transcript + diffs.

## 5. Components

### 5.1 Orchestrator API

Node.js/TypeScript (or Python/FastAPI) HTTP+WebSocket service. Responsibilities:
- CRUD for Projects/Goals/Tasks.
- `POST /tasks/:id/approve`, `/reject`, `/answer` — resolves an Approval, flips Task state, signals the scheduler.
- `GET /stream/:taskId` (SSE) or a single multiplexed `/ws` — live event feed for the dashboard.
- Auth: single-user token auth is sufficient initially (local/self-hosted), but keep it behind an API key from day one since it executes shell commands.

### 5.2 Scheduler

Runs as a loop (or reactive on event bus) inside the orchestrator process:
1. Maintain two **lanes**: `supervisor` (concurrency = configurable, e.g. 3 — cloud APIs handle concurrency fine) and `worker` (concurrency = configurable, e.g. 1–2 — bounded by local GPU/CPU).
2. Each lane has a queue of `ready_for_work`/`planning`-eligible tasks across **all projects**, ordered by `(priority desc, created_at asc)` — fair scheduling, not project-exclusive, so 5 projects share the 2 worker slots round-robin as capacity frees up.
3. When a lane has a free slot, pop the next eligible task, mark it `in_progress`/`planning` (with a `locked_by` worker id + `lease_expires_at` for crash safety), and dispatch to the relevant Engine Adapter.
4. On task entering `awaiting_input`, the slot is freed immediately — the scheduler does not wait, it moves on to the next queued task. This is the core "don't block the queue on human input" requirement.
5. Lease-based locking: every dispatched task has a heartbeat; if a worker process dies, the lease expires and the task is requeued automatically (resilience).

### 5.3 Engine adapters

A small interface both engines implement, so the scheduler doesn't care which one runs a task:

```ts
interface EngineAdapter {
  start(run: RunContext): AsyncIterable<EngineEvent>; // streams tokens/tool-calls/diffs
  cancel(runId: string): Promise<void>;
}
```

- **ClaudeCodeAdapter** (supervisor lane): spawns `claude -p <prompt> --output-format stream-json --permission-mode plan|acceptEdits` (or uses the Claude Agent SDK directly in-process for tighter control/hooks), parses the streamed JSON events into our `EngineEvent` shape, captures the produced plan / review verdict from the final structured message.
- **AiderAdapter** (worker lane): spawns `aider --yes --message "<instruction from plan>" --model <local-model-via-gateway>` against the project's git worktree, captures stdout/diff events, and resolves when Aider commits or reports it's stuck (Aider's own "I don't know how to proceed" outputs map to `awaiting_input`/`needs_fix`).
- Both adapters run inside per-task **git worktrees** (`git worktree add ../runs/<task-id>`) so concurrent tasks on the same project never clobber each other's working directory; successful diffs are merged back (fast-forward or PR-style merge task) under supervisor approval.

### 5.4 LLM Gateway

Run **LiteLLM proxy** (or a small custom Express/FastAPI shim if more control is needed) as a single OpenAI/Anthropic-compatible endpoint that both engines talk to. Why a gateway instead of pointing engines directly at providers:
- One place to set **per-provider timeouts** — this matters a lot here: local Ollama/LocalAI calls may legitimately take minutes (large context, CPU inference, model swap). Configure the gateway with provider-specific timeouts (e.g. local: 600–1800s connect-and-read timeout with streaming keep-alive; cloud: 120s) instead of a single global value.
- One place for **retry/backoff** (e.g. local endpoint cold-start failures, transient 503s) and **circuit breaking** (if local host is down, fail fast and mark tasks `awaiting_input`/`failed` instead of hanging the lane).
- One place to log token usage/cost per call for the cost dashboard.
- Lets you swap Ollama → LocalAI → vLLM later without touching engine adapters.

### 5.5 Event bus & streaming

- In-process pub/sub (e.g. Node `EventEmitter` or a tiny Redis pub/sub if multi-process) fans out `EngineEvent`s as they arrive from adapters.
- Every event is (a) persisted to `events` table immediately (durability — dashboard reconnecting after a crash sees full history), and (b) pushed to any subscribed WebSocket/SSE clients (liveness).
- Dashboard subscribes per-task or to a project/global firehose; reconnect simply re-fetches `events since last_seen_id` then resumes the live tail — no lost output across network blips or orchestrator restarts.

### 5.6 Persistence & workspaces

- Postgres for all structured state (projects/tasks/runs/events/approvals/skills) — gives transactional task-claiming (`SELECT ... FOR UPDATE SKIP LOCKED`) which is the simplest correct way to implement the lease-based scheduler without a separate queue broker.
- Local filesystem (or S3-compatible bucket) for large artifacts: full diffs, build logs, test output.
- One git worktree per in-flight Run, under `~/.orchestrator/workspaces/<project>/<task-id>`, cleaned up after merge.

### 5.7 Observability

- **Structured logging**: every log line carries `project_id`, `task_id`, `run_id`, `lane` — pino/winston (Node) or structlog (Python) with JSON output, shipped to stdout (captured by whatever process manager) and optionally Loki.
- **Tracing**: OpenTelemetry spans for `task.lifecycle`, `engine.run`, `llm.call` (with provider/model attributes and latency — crucial for spotting a slow local model run vs. a hung HTTP call), exported to a local Jaeger/Tempo instance or just console in dev.
- **Metrics**: queue depth per lane, task throughput, average local-model latency, error/retry counts, cost per project — Prometheus `/metrics` endpoint, optional Grafana dashboard.
- **Dashboard "system health" panel**: lane utilization, last heartbeat per worker, gateway circuit-breaker state.

### 5.8 Resilience checklist

- Long, provider-specific HTTP timeouts for local models; cloud calls keep short timeouts.
- Heartbeat/lease on every in-flight task; orchestrator restart or worker crash auto-requeues leased-but-stale tasks.
- Idempotent task steps: a retried Run starts from the last committed git state, not from scratch, so partial progress isn't lost.
- Circuit breaker around the local-model provider: N consecutive failures → open circuit → new worker tasks go to `awaiting_input` ("local model host unreachable") instead of piling up retries.
- Dead-letter state + max-retry cap so a broken task can't infinite-loop burning local compute.
- Graceful shutdown: SIGTERM drains in-flight runs (or checkpoints them) before exit; orchestrator is safe to restart anytime (all state is in Postgres, nothing lives only in memory).
- Everything idempotent enough that the whole orchestrator can be a single `systemd`/Docker-restart-always service.

## 6. Tech stack recommendation

| Layer | Choice | Why |
|---|---|---|
| Orchestrator | Node.js + TypeScript (Fastify) | Same ecosystem as Claude Agent SDK (TS), good WS/SSE support, easy to keep single codebase with dashboard. |
| DB | Postgres | `SKIP LOCKED` task claiming, JSONB for plans/events, mature. |
| LLM gateway | LiteLLM proxy | Drop-in OpenAI/Anthropic-compatible router for Ollama/LocalAI/Anthropic/OpenAI; saves writing provider adapters by hand. |
| Supervisor engine | Claude Agent SDK (TS) or `claude -p --output-format stream-json` | Native Claude Code planning/verification/skill-authoring. |
| Worker engine | Aider (subprocess) | Mature local-model code editing, git-native checkpoints. |
| Dashboard | Next.js/React + WS client | Simple live task tree + token stream + approve/reject buttons. |
| Process mgmt | Docker Compose (orchestrator, Postgres, LiteLLM, Ollama, Jaeger) | One `docker compose up -d`, always-on. |
| Tracing/metrics | OpenTelemetry SDK → Jaeger/Tempo, Prometheus `/metrics` | Local-first observability stack. |

## 7. Implementation plan (phased)

**Phase 0 — Spike (1–2 days)**
- Confirm `claude -p --output-format stream-json` and Aider's `--message`/`--yes` non-interactive modes behave as expected; confirm LiteLLM proxy can front an Ollama model with Anthropic- and OpenAI-style requests and survives a 10+ minute generation.
- Decide final language (assume TS below).

**Phase 1 — Single-project MVP (no queueing yet)**
- Postgres schema + migrations for projects/tasks/runs/events.
- `ClaudeCodeAdapter` and `AiderAdapter` implementing the `EngineAdapter` interface, each tested standalone against one local repo.
- Minimal CLI: `orchestrator run-goal "<goal>" --project foo` that does plan → implement → verify once, sequentially, logging to stdout.
- LiteLLM gateway wired in for both engines.

**Phase 2 — Scheduler & multi-project queue**
- Implement lease-based task claiming (`FOR UPDATE SKIP LOCKED`) and the two-lane scheduler.
- Support N projects queued simultaneously; verify that an `awaiting_input` task frees its slot immediately and the next ready task starts.
- Approval API endpoints (approve/reject/answer) wired to unblock tasks.

**Phase 3 — Streaming dashboard**
- Event bus + WebSocket/SSE server; persist+broadcast every engine event.
- Next.js dashboard: project/task tree, live token stream pane per active run, approval inbox, basic system-health panel.

**Phase 4 — Resilience hardening**
- Per-provider timeout/retry/circuit-breaker config in the gateway.
- Heartbeats + stale-lease requeueing; crash-and-restart test (kill orchestrator mid-run, confirm clean resume).
- Dead-letter handling + max retries.

**Phase 5 — Observability**
- Structured logging with correlation IDs everywhere.
- OpenTelemetry traces for task lifecycle + LLM calls; Jaeger wired via Docker Compose.
- Prometheus metrics + simple Grafana dashboard (queue depth, latency, cost).

**Phase 6 — Supervisor autonomy features**
- Goal decomposition prompt templates (supervisor turns a Goal into Tasks + acceptance criteria).
- Skill authoring/storage and injection into worker prompts.
- Verification prompt templates (diff + test output → pass/fail + follow-up Task creation).

**Phase 7 — Polish**
- Priority/SLA controls per project, cost budgets/alerts, multi-user auth if needed, packaging (single Docker Compose bundle + setup script).

## 8. Open questions for the user

1. Preferred orchestrator language: TypeScript (pairs naturally with Claude Agent SDK + a Next.js dashboard) or Python (pairs naturally with most local-model tooling)?
2. Should the worker tier's first local-model target be Ollama specifically, or also LocalAI/vLLM from day one?
3. Is a self-hosted single-user setup acceptable (no auth beyond an API key), or is multi-user/remote access needed from the start?

**Addendum**: Question 2 is resolved — LocalAI is now supported alongside Ollama as a
day-one local-model backend (routed through LiteLLM via `model: openai/<model-name>` +
`api_base`), and both the supervisor and worker lanes default to LocalAI models so the
whole pipeline runs fully offline by default. The cloud (Anthropic) entry remains
available as an explicit opt-in per project.
