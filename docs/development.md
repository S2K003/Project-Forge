# Development

## Prerequisites

- .NET SDK 10.0.4xx (pinned in `global.json`)
- (Live Mode only) [Ollama](https://ollama.com) with `qwen2.5-coder:7b` pulled (or another — `Ai:Model`)

## Build & test

```bash
dotnet build
dotnet test
```

## Run

### Demo Mode only (no backend)

```bash
dotnet run --project src/ForgeOps.Web
```

Open the printed URL, choose **Demo Mode**, and walk the CustomerHub journey. Nothing
else needs to be running.

### Live Mode (frontend + API + local Ollama)

```bash
# terminal 1 — local model
ollama serve
ollama pull qwen2.5-coder:7b   # first time only

# terminal 2 — API
dotnet run --project src/ForgeOps.Api          # http://localhost:5xxx

# terminal 3 — frontend, pointed at the API
#   edit src/ForgeOps.Web/wwwroot/appsettings.json:
#   { "ForgeOps": { "ApiBaseUrl": "http://localhost:<api-port>" } }
dotnet run --project src/ForgeOps.Web
```

Then choose **Live Mode**. If Ollama is not running you get the Connection Gate — that is
the designed behaviour.

In Live Mode the **Requirement step is an editable text box** — type whatever you want to
build. ForgeOps classifies it (UI component vs backend logic) and runs the same journey:
spec → generate → deterministic audit → human decision → render / execute → health.

## Solution layout

```
src/
  ForgeOps.Web            Blazor WebAssembly (standalone, static) — the Vercel deployable
  ForgeOps.Api            ASP.NET Core minimal API — health, AI bridge proxy, spec + forge
  ForgeOps.AI             AI Gateway, IAiProvider, OllamaBridgeProvider, CodeGenerator, telemetry
  ForgeOps.Forge          Roslyn compile, BannedApiScanner, SandboxRunner, canonical acceptance suite
  ForgeOps.Forge.Sandbox  short-lived child process that runs generated tests (built, copied next to the API)
  ForgeOps.Demo           CustomerHubJourney — the single source of truth for the walkthrough
  ForgeOps.Contracts      DTOs and enums shared by Web and Api
tests/
  ForgeOps.UnitTests      circuit breaker, output validation, banned-API scan,
                          forge pipeline (compiles + sandbox-runs the reference impl), journey integrity
```

The `CodeRunner` section in `ForgeOps.Api` config controls execution of generated code:
`Enabled` (default true locally; set false on any shared host), `TimeoutSeconds`,
`MaxRepairAttempts`.

Later phases add `ForgeOps.Domain`, `ForgeOps.Application`, `ForgeOps.Infrastructure`,
`ForgeOps.GitHub`, `ForgeOps.Analysis`, `ForgeOps.Quality`, `ForgeOps.Observability`
and the remaining test projects (see the README Roadmap).

## Config surfaces

| Setting | Where | Notes |
|---|---|---|
| `ForgeOps:ApiBaseUrl` | `ForgeOps.Web/wwwroot/appsettings.json` | empty ⇒ Demo Mode only. The **only** thing the browser is told. |
| `Ai:*` | `ForgeOps.Api` config / env | provider, bridge URL, model, token, timeouts — server-side only |
| `Cors:AllowedOrigins` | `ForgeOps.Api` config / env | the Vercel domain(s); localhost is allowed automatically in dev |
