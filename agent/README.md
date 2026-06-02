# AutoFlow Agent

Runs on the user's machine and executes automations dispatched by the backend.
It dials out to the backend over SignalR (no inbound ports), runs web steps with
Playwright, and streams progress back.

## Prerequisites

- .NET 8 SDK
- The backend running (see repo root `docker compose up`)

## First-time setup

```bash
cd agent
dotnet build
# Install the Chromium browser Playwright drives (one-time):
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
# (No PowerShell? Use the CLI tool instead:)
#   dotnet tool install --global Microsoft.Playwright.CLI
#   playwright install chromium
```

## Configure

Edit `appsettings.json` (or use environment variables, e.g. `Agent__BackendUrl`):

- `BackendUrl` — where the backend is reachable (default `http://localhost:8080`)
- `Token` — must match the backend's `AGENT_SHARED_TOKEN`
- `UserEmail` — which user this agent belongs to (MVP: `demo@amcor.com`)
- `Headless` — `false` (default) lets you watch the browser work

## Run

```bash
dotnet run
```

On start it connects, registers, and the header in the web app flips **Agent: Connected**.
Click **Run now** on an active automation and the steps execute here.

## Notes / MVP limits

- Web actions are implemented (open browser, navigate, click, type, select, wait).
- `read_email`, `extract`, `condition`, `loop`, `api_call` are reported as *skipped* —
  these are the next executors to build.
- Native (non-browser) apps are launched best-effort via the OS; rich desktop control
  (UI Automation / FlaUI) is the planned next step.
- Variable substitution (`{{name}}`) is not resolved yet; values pass through literally.
- Not containerized: the agent must run in the user's interactive desktop session to
  drive their applications.
