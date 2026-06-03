# AutoFlow — Project Context (read me first)

This file is the working brief for any AI/dev session on this repo. It explains what
AutoFlow is, the architecture, the standards we hold to, and what we're building next.

---

## 1. What this is

Amcor AutoFlow lets non-technical staff describe an automation in plain language (ordered
steps + optional screenshots). Claude compiles that into a versioned, engine-agnostic
**IR** (intermediate representation). A **local agent** running on the user's PC executes
the IR. Web automation works today; the current milestone is **native Windows / Office
automation** (open Excel/Word, open/save files, extract data, drive desktop apps).

## 2. Architecture (and the one non-negotiable)

```
React UI ──HTTP──► .NET API (N-tier) ──► Claude API (compile steps → IR)
                        │  └── Postgres (IR as JSONB) + MinIO (screenshots)
                        │
                   SignalR hub ◄── outbound WS ── Local Agent (.NET worker)
                                                     └── executes the IR
```

**Non-negotiable execution model:** the agent runs in the **user's interactive desktop
session** and is the *only* thing that touches their applications. The server never drives
apps. The agent dials **out** over SignalR (no inbound ports). This is why the agent is not
in docker-compose and why all "use a Windows app" work happens in `agent/`.

**The IR is the contract.** Frontend, backend, Claude, and the agent all agree on one JSON
shape. Nothing emits raw Playwright/UIA code — the agent *interprets* the IR. The DB is the
source of truth for the IR; the agent receives it over SignalR per run.

## 3. Tech stack & repo map

- **backend/** — .NET 8, strict N-tier:
  - `AutoFlow.Domain` — entities + enums
  - `AutoFlow.Application` — DTOs, the IR model (`Ir/AutomationIr.cs`), interfaces, use-case services
  - `AutoFlow.Infrastructure` — EF Core/Npgsql, repository, **Claude client** (`Claude/ClaudeCompilationService.cs`), MinIO, **IR validation** (`Validation/IrValidator.cs`)
  - `AutoFlow.Api` — controllers, **SignalR hub** (`Realtime/AgentHub.cs`), `Program.cs`
- **frontend/** — React + TS + Vite + Tailwind, react-query, component-driven (`src/{lib,api,hooks,components,pages,types}`)
- **agent/** — .NET 8 worker. **This is where the new work lives.**
  - `Hub/AgentConnection.cs` — SignalR client; receives `RunAutomation`, streams `ReportStep`/`RunCompleted`
  - `Execution/IStepExecutor.cs` — orchestrator entry point
  - `Execution/PlaywrightExecutor.cs` — **current** executor: a `switch (step.Action)` plus an inner `BrowserSession` (owns Playwright browser/page; `OpenApplicationAsync` decides browser vs `Process.Start`)
  - `Models/AutomationIr.cs` — agent-side IR mirror (params is an open dictionary)
- **contracts/automation-ir.schema.json** — the canonical IR JSON Schema
- **db/init/01_schema.sql** — Postgres schema (source of truth for the MVP)

## 4. The IR contract — the heart of the system

Current action vocabulary:
`open_application, navigate, click, type_text, select_option, read_email, extract, wait,
condition, loop, api_call`.

A step looks like: `{ id, order, action, target?{app,url,selector,label}, params{}, rawInstruction, needsClarification, clarificationQuestion }`.

**Critical rule:** the IR is defined in **four places that MUST stay in sync**. Any change
to actions/params touches all four in the same PR:
1. `contracts/automation-ir.schema.json` (the human-facing contract)
2. `backend/.../Infrastructure/Validation/IrValidator.cs` → embedded `SchemaText` (server validates every compiled IR against this before storing)
3. `backend/.../Infrastructure/Claude/ClaudeCompilationService.cs` → `SystemPrompt` (so Claude knows the new actions and when to ask for clarification) **and** `ToolInputSchema` (the forced tool-use input schema)
4. `agent/.../Execution/...` handlers (so the agent can actually run it)

Keep the vocabulary **engine-agnostic** — describe *intent* (`open_application` app="excel"),
not implementation. The agent decides how (OpenXML vs COM vs UIA) at runtime.

## 5. Current state (works today)

Create automation → compile (Claude → IR, validated, versioned in `automation_versions.definition` JSONB)
→ answer clarifications → activate → "Run now" dispatches IR to the connected agent → agent
runs **web** steps (open Chrome, navigate, click, type, select, wait) and streams status,
which the UI polls. Confirmed working end-to-end.

## 6. Engineering standards (hold the line)

- **Modular, single-responsibility.** No more growing the giant `switch`. New behavior = a
  new small, injectable, testable class — not another case.
- **DI everywhere**, program to interfaces, constructor injection.
- **N-tier boundaries respected** on the backend (Domain ← Application ← Infrastructure/Api;
  Application defines ports, Infrastructure/Api implement them).
- **Async/cancellation** threaded through (`CancellationToken`).
- **Tests** for new units (handlers, resolvers, services). The patterns below are designed
  to make this easy.
- **Security mindset:** the agent executes arbitrary instructions on a user's machine. New
  capabilities must consider path sandboxing, an action policy/allowlist, and never putting
  secrets in the IR.
- **Don't break web automation** — it's validated and in use. Refactors must be behavior-
  preserving for existing actions.

## 7. Next milestone — native Windows / Office automation

### 7a. Do the foundation refactor first (Phase 1)

The current `PlaywrightExecutor` (one switch + one browser session) won't scale to multiple
"surfaces" (web, desktop, Office). Refactor `agent/Execution/` to:

```
Execution/
  IStepExecutor.cs            (keep — orchestrator entry point AgentConnection calls)
  RunExecutor.cs              (NEW — the per-step loop; owns ExecutionContext; routes each step to a handler; preserves StepReport streaming)
  ExecutionContext.cs         (NEW — Variables dictionary, open Sessions, RunId, CancellationToken, optional evidence/asset sink)
  Variables/VariableResolver.cs (NEW — resolve {{name}} from context.Variables)
  Handlers/
    IActionHandler.cs         (NEW — string Action {get;}  Task ExecuteAsync(IrStep step, ExecutionContext ctx))
    ActionHandlerRegistry.cs  (NEW — resolves a handler by action name; populated via DI)
    OpenApplicationHandler.cs, NavigateHandler.cs, ClickHandler.cs, TypeTextHandler.cs,
    SelectOptionHandler.cs, WaitHandler.cs, ExtractHandler.cs, ...
  Sessions/
    ISession.cs / ISessionFactory.cs
    WebSession.cs             (the current BrowserSession, lifted out)
    DesktopSession.cs         (NEW — see 7c)
    OfficeService.cs          (NEW — see 7c)
```

Each action becomes one handler class (testable in isolation). `OpenApplicationHandler`
inspects `target.app` and lazily creates/opens the right session, stored in
`ExecutionContext` so several surfaces can be live in one run (e.g. extract from browser →
write to Excel). Unsupported actions still report "skipped" via the registry's fallback.

### 7b. Make data flow real (Phase 2 — prerequisite for anything useful)

Today `{{variable}}` substitution is a no-op and `extract` is skipped. For "extract in
browser, save to Excel" you need:
- `VariableResolver` that replaces `{{name}}` in params using `ExecutionContext.Variables`.
- A real `ExtractHandler` that reads a value (web: text/attribute of a selector; Office: a
  cell/range) and stores it under the step's declared variable name.
- IR `variables[]` honored end-to-end.

### 7c. Add the Windows surfaces (Phase 3)

**Hard product requirement: automation must be VISIBLE.** Users only trust it if they see
the real app open and elements get clicked, exactly like the browser flow. This rules out
headless file manipulation as the default. It must also work for **any** Windows app the
user names — Office is just an example set, not the scope.

Primary engine: **Windows UI Automation (UIA)** via **FlaUI** (`FlaUI.UIA3`). UIA is
Microsoft's accessibility layer, so it drives the real, on-screen window of essentially any
native app (Win32/WinForms/WPF/UWP): launch the app, find elements by name/automationId/
control type, click and type visibly. `open_application` takes an app alias or full path;
the agent resolves aliases via a configurable map and accepts explicit paths.

Library roles (pin current stable NuGet versions — verify, don't trust memory):
- **FlaUI (`FlaUI.UIA3`)** — primary, general-purpose, visible desktop driver for any app.
- **COM Interop (`Microsoft.Office.Interop.*`)** — reliability fallback for data-heavy Office
  work; set `Visible = true` so the window updates on screen. Looks like the app acting on
  its own rather than clicking, so not the default.
- **ClosedXML / DocumentFormat.OpenXml** — headless (invisible) file ops. Deprioritized given
  the visibility requirement; keep only as a silent fallback if visibility is ever relaxed.

Coverage reality (design for graceful degradation):
- UIA trees are rich for well-behaved native apps, thin for canvas/custom-rendered apps
  (some Electron/Java apps). Fallback order: UIA element → **keyboard-driven** (Tab/Enter/
  hotkeys, still visible) → **coordinate/image-based** click (last resort). Handlers should
  record which strategy they used.
- Excel's grid is a single UIA control; do per-cell work visibly via the Name Box + keyboard
  rather than clicking individual cells.

Keep the IR vocabulary **intent-based and engine-agnostic** (`open_application`, `click`,
`type_text`, `extract`, …); the agent chooses UIA vs keyboard vs COM at runtime. Proposed
new actions to wire through all four touchpoints (§4): `open_file`, `save_file`,
`press_keys`, `focus_window`, `read_value`/`extract` (real), `wait_for` (element/condition).

### 7d. Productionize (Phase 4, after Windows works)

- **Run evidence:** capture per-step screenshots in the agent, upload to MinIO, populate
  `run_step_logs.screenshot_object_key` (column already exists) for an audit trail.
- **Robustness:** per-step timeout, retries with backoff, optional continue-on-error, clear
  failure messages.
- **Security/policy:** action allowlist, file-path sandboxing, secret handling via Windows
  Credential Manager / a secrets store (never in the IR).

## 8. Known gaps / backlog (not this milestone, but where we're heading)

- **Auth:** replace the seeded `demo@amcor.com` user + shared agent token with real SSO. The
  `agents` table + `token_hash` column already exist for per-agent credentials.
- **Agent registry & selection:** persist agents, heartbeat/`last_seen`, pick the right
  machine when a user has several. Tracker is currently in-memory, keyed by user email.
- **Scale-out:** the in-memory `IAgentConnectionTracker` needs a **SignalR Redis backplane**
  for multi-instance backend.
- **Run history:** add a list endpoint + UI (today the UI polls a single run by id).
- **Triggers run IN THE AGENT, not the server.** The email lives in the user's local Outlook
  and files live on their disk, so trigger detection mirrors execution — it belongs agent-side.
  Design: on connect (and on change) the backend pushes the user's active non-manual triggers
  to the agent (type + match spec: subject-contains, folder, file glob, cron). The agent runs
  watchers; when one fires it notifies the backend with captured context (which becomes run
  variables), and the backend records a run and dispatches the IR back — identical to a manual
  run from there, so it stays versioned/auditable.
  - **Email trigger (no tenant admin, no Graph):** connect to the running **classic** Outlook
    desktop via its **COM Object Model** using the user's own profile; subscribe to
    `Application.NewMailEx` / `Items.ItemAdd` (or poll with a `Restrict` filter). The same COM
    access reads sender/subject/body/attachments for downstream `extract`.
  - **⚠ Sunset risk — design around it:** COM/OOM/VSTO/MAPI work on **classic** Outlook only.
    **New Outlook for Windows supports none of them** (it's web-based, shares the OWA codebase).
    Microsoft made new Outlook the enterprise default ~April 2026; classic is supported until
    ~2029 and users can still revert. So treat **classic Outlook as a detected prerequisite**
    for the email trigger, surface a clear message if the user is on new Outlook, and do not
    let any other feature depend on Outlook COM. Long-term, the only mailbox-event path is
    Graph (admin/consent) — out of scope under current constraints.
  - **File-created** triggers: `FileSystemWatcher`. **Schedule** triggers: a local timer/cron.
    These are durable and admin-free.

## 9. Conventions & gotchas

- DB enums are stored as **snake_case text + CHECK** and mapped via converters in
  `AutoFlowDbContext` (e.g. `NeedsClarification` ↔ `needs_clarification`).
- IR lives in `automation_versions.definition` as **JSONB** (GIN-indexed).
- SignalR JSON is **case-insensitive** on deserialize; the agent IR mirror uses
  `[JsonPropertyName]` to be safe — keep it aligned with the server IR's camelCase.
- Screenshots travel from the browser as **data URLs**; the backend strips the `data:` prefix.
- The agent **must** run in the interactive session; never try to containerize it.
- After changing the IR, re-validate the worked example in `contracts/examples/`.