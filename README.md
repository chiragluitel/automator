# Amcor AutoFlow

Internal, AI-assisted automation builder. Non-technical staff describe an automation
as ordered steps (free text + screenshots). Claude compiles those into a versioned,
engine-agnostic **IR** (intermediate representation). A lightweight **agent** on each
user's PC executes the IR — web steps via Playwright, native Windows steps later.

## Architecture (local-agent model)

```
                        ┌──────────────────────────────────────────┐
                        │            SERVER  (docker compose)        │
   ┌──────────┐  HTTP   │  ┌───────────┐   ┌──────────────────────┐ │
   │  React   │◄───────►│  │  .NET API │──►│  Claude API (compile) │ │
   │ frontend │  (REST/ │  │  N-Tier   │   └──────────────────────┘ │
   └──────────┘  poll)  │  │           │   ┌──────────┐  ┌────────┐ │
                        │  │ SignalR ◄─┼──►│ Postgres │  │ MinIO  │ │
                        │  │   hub     │   │ (IR JSONB│  │(screens│ │
                        │  └─────┬─────┘   │  + runs) │  │ -hots) │ │
                        └────────┼─────────┴──────────┴──┴────────┘ │
                                 │ outbound WebSocket (agent dials out)
                        ┌────────▼─────────┐
                        │  Local Agent      │  user's interactive session
                        │  .NET worker      │
                        │  └ Playwright     │  ← web steps
                        └───────────────────┘
```

- **IR is the contract.** Frontend, backend, Claude, and the agent share one JSON
  shape (`contracts/automation-ir.schema.json`). Nothing emits raw Playwright code;
  the agent *interprets* the IR. The backend validates every compiled IR against the
  schema before storing it.
- **The agent is NOT in docker-compose.** It runs on the user's machine to drive their
  apps. Compose spins up the server side only.
- **Comms are outbound-only.** The agent opens a SignalR connection; the backend pushes
  run dispatches and receives step reports.

## Repo layout

```
amcor-autoflow/
├─ docker-compose.yml            # postgres + minio + backend + frontend
├─ .env.example                  # copy to .env and fill in
├─ db/init/01_schema.sql         # Postgres schema (source of truth for the MVP)
├─ contracts/
│  ├─ automation-ir.schema.json  # THE contract (JSON Schema 2020-12)
│  └─ examples/access-request.ir.json
├─ backend/                      # .NET 8 N-Tier API + SignalR hub
│  └─ src/
│     ├─ AutoFlow.Domain/        # entities + enums
│     ├─ AutoFlow.Application/   # DTOs, IR model, interfaces, use-case services
│     ├─ AutoFlow.Infrastructure/# EF Core, repos, Claude client, MinIO, validation
│     └─ AutoFlow.Api/           # controllers, hub, Program.cs
├─ frontend/                     # React + TS + Tailwind + react-query
│  └─ src/{lib,api,hooks,components,pages,types}
└─ agent/                        # .NET 8 worker: SignalR client + Playwright executor
```

## Run the server

```bash
cp .env.example .env
#   - set ANTHROPIC_API_KEY
#   - set strong POSTGRES_* / MINIO_* values
#   - keep AGENT_SHARED_TOKEN in sync with agent/appsettings.json
docker compose up --build
```

- Frontend: http://localhost:5173
- API + Swagger: http://localhost:8080/swagger
- MinIO console: http://localhost:9001
- Postgres: localhost:5432

## Run the agent (on the user's PC)

See `agent/README.md`. In short:

```bash
cd agent
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium   # one-time
dotnet run
```

When connected, the web app header shows **Agent: Connected**.

## End-to-end flow

1. **Create** an automation in the builder: add steps (plain text + optional screenshots),
   describe the trigger.
2. **Compile** — the backend sends steps + screenshots to Claude, which returns the IR
   (forced via a tool call), validated against the schema and stored as a version.
3. **Clarify** — any steps Claude couldn't pin down come back with questions; answer them
   and re-compile.
4. **Activate** the version once there are no open questions.
5. **Run now** — the backend dispatches the IR to your connected agent; the agent executes
   it and streams step status back, which the UI polls and displays.

## Intentionally minimal for the MVP (and what's next)

- **No SSO.** A seeded demo user (`demo@amcor.com`) and a shared agent token stand in for
  auth. Swap `ICurrentUser` + the hub's token check for real identity.
- **UI polls run status** instead of opening a second realtime channel to the browser.
- **Web-first executor.** Native desktop control (UI Automation / FlaUI), `read_email`,
  `extract`, conditions/loops, and `api_call` are stubbed/skipped — the next executors.
- **Variable resolution** (`{{name}}`) is not implemented yet.
- **Single backend instance.** The in-memory agent connection tracker needs a SignalR
  backplane (e.g. Redis) to scale out.
- **Email/scheduled triggers** are modeled in the IR but not yet wired; manual "Run now"
  is the implemented path. Email triggers will use Microsoft Graph subscriptions server-side.

## Note

This codebase was authored as a complete, runnable MVP but has not been compiled/run in
this environment. Treat the first `docker compose up` + agent run as the integration test.
