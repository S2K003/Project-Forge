<h1>ForgeOps</h1>

**An AI-assisted engineering control plane for modern .NET teams — built zero-budget.**

ForgeOps turns a one-line requirement into governed, testable, reviewable engineering
work, keeping **deterministic evidence and human judgment above every AI recommendation**.
Every AI capability runs on a **self-hosted local model** (`qwen3:8b` via Ollama), and
every hosted component runs on a **free tier**.

```
AI Recommendation  →  Deterministic Evidence  →  Human Judgment  →  Engineering Decision
```

---

## The two modes

| | **Demo Mode** | **Live Mode** |
|---|---|---|
| Purpose | Show the complete product story, anywhere, anytime | The real product doing real work |
| Backend | none — fixture compiled into the WASM bundle | real API + real AI Bridge call to `qwen3:8b` |
| AI steps | clearly-labelled **recordings** (`Simulated` tag) | live model calls, never faked |
| If the AI Bridge is offline | unaffected | app locks behind the **Connection Gate**; one click to Demo Mode |
| Route | `/demo` | `/live` |

Same journey, same components, same UI — only the data source and the mode badge differ.

## The journey (CustomerHub)

> *"Customers should receive loyalty points after successful purchases."*

Sign in → create requirement → **AI specification** → human review → **AI implementation**
(the model writes `LoyaltyService` + tests, with a compile-error repair loop) →
**deterministic audit** (Roslyn compile, analyzers, banned-API scan, architecture) →
quality gates → **AI review** → **human decision** (approve execution) → **run & verify**
(the sandbox executes ForgeOps' own acceptance suite against the generated code and maps
every result back to an acceptance criterion) → merge → telemetry → **engineering
health** with a full "Why?".

The payoff is real: ForgeOps generates working code and *proves* it satisfies the
requirement by running it. The §31 bug is genuinely detectable — a weak implementation
passes the model's own tests but fails ForgeOps' canonical AC-2 "duplicate payment event"
test.

**UI requirements too.** A requirement classified as UI (e.g. *"show a customer's loyalty
status as a compact card"*) generates a self-contained HTML component, which is audited
(no network / storage / eval / external resources) and then **rendered live in a
locked-down sandboxed iframe** with the model's behavioural checks running against it. Two
scenarios ship: `loyalty rules` (backend logic) and `loyalty status card` (UI) — both work
in Demo and Live.

---

## Quick start

```bash
# Demo Mode — nothing else required
dotnet run --project src/ForgeOps.Web      # open the URL, pick "Demo Mode"
```

```bash
# Live Mode — needs the local model
ollama serve && ollama pull qwen3:8b
dotnet run --project src/ForgeOps.Api
# set ForgeOps.Web/wwwroot/appsettings.json → ApiBaseUrl, then:
dotnet run --project src/ForgeOps.Web
```

Full detail: [docs/development.md](docs/development.md).

## Architecture

Modular monolith. Slice 1 projects:

```
ForgeOps.Web            Blazor WebAssembly (static)  ── deployed to Vercel
ForgeOps.Api            ASP.NET Core minimal API     ── deployed to a free-tier host
ForgeOps.AI             AI Gateway · IAiProvider · OllamaBridgeProvider · CodeGenerator · validation · telemetry
ForgeOps.Forge          Roslyn compile · banned-API audit · sandbox runner · canonical acceptance suite
ForgeOps.Forge.Sandbox  the short-lived child process that executes generated tests
ForgeOps.Demo           CustomerHubJourney — single source of truth for both modes
ForgeOps.Contracts      shared DTOs / enums
```

- [docs/ai-architecture.md](docs/ai-architecture.md) — the AI Bridge & tunnel, step by step
- [docs/hosting.md](docs/hosting.md) — the free-tier topology and how to swap any piece
- [docs/decisions/0001-slice-1-scope.md](docs/decisions/0001-slice-1-scope.md) — slice 1 scope
- [docs/decisions/0002-generated-code-execution.md](docs/decisions/0002-generated-code-execution.md) — generating & running code, and its guardrails

## Cloud deployment

| Piece | Host | Config |
|---|---|---|
| `ForgeOps.Web` | Vercel (static) | `vercel.json` + `deploy/vercel-build.sh`; set `FORGEOPS_API_BASE_URL` to enable Live Mode |
| `ForgeOps.Api` | Render / Fly.io free tier | `src/ForgeOps.Api/Dockerfile` |
| AI inference | your PC — Ollama `qwen3:8b` | reached via Cloudflare Tunnel / ngrok |
| Database | Neon / Supabase free Postgres | *(Roadmap)* |

Deploying the frontend alone gives a fully working **Demo Mode** site.

## Testing

```bash
dotnet test
```

Slice 1: circuit-breaker behaviour, deterministic AI-output validation, and journey
integrity (every step kind present, blocking gate exists, all recorded AI flagged
simulated, health weights sum to 1).

## Security & AI safety

- Requirement text is treated as **untrusted input**; system instructions, trusted context
  and untrusted content are kept in separate zones in every prompt.
- The AI Bridge tunnel is authenticated — Ollama is never exposed directly.
- AI cannot merge PRs, deploy, override a deterministic gate, or decide to ship code.
- **Generated code** is gated before it runs: a deterministic banned-API scan (process /
  filesystem / network / interop / unsafe), compilation against a curated reference set,
  then execution only as test methods in a separate short-lived process with a wall-clock
  budget. A human approves execution of the exact code they reviewed. See
  [ADR 0002](docs/decisions/0002-generated-code-execution.md) for the full guardrails and
  their limits. Set `CodeRunner:Enabled=false` on a shared host to withhold execution
  entirely.
- Invalid AI structured output never enters the domain — it is rejected by a deterministic
  validator and recorded as such.

## Roadmap

1. **Slice 3** — `ForgeOps.Domain/Application/Infrastructure`, EF Core + free Postgres,
   persist journeys and forge runs, auth foundation.
2. **Slice 4** — broaden the generation target; stronger sandbox isolation (memory caps /
   Job Objects / container). Real deterministic analysers on the wider codebase.
3. **Slice 5** — GitHub read-only ingestion (repos, PRs, changed files, CI status,
   webhook validation); open the generated implementation as a real PR.
4. **Slice 6** — `ForgeOps.Observability`, OTLP export to a free sink;
   `IntegrationTests` / `EndToEndTests` (Playwright).
5. **Slice 7** — optional Azure path (Key Vault, Azure Database for PostgreSQL).

## Trade-offs & limitations

- The generation target is deliberately **one component against a fixed contract** so an
  8B local model succeeds reliably. A compile-error repair loop (max 2 rounds) absorbs the
  common failures; if it still doesn't compile, the audit step shows why.
- The sandbox is a static-gate + curated-references + separate-process + timeout design —
  solid for trusted-ish model output, **not** a hostile-code sandbox. Hard memory/OS
  isolation is slice 4. See [ADR 0002](docs/decisions/0002-generated-code-execution.md).
- The AI *review* step and engineering-health score use seeded data in Live Mode; the
  **specification, implementation, audit and acceptance run are real**.
- No persistence yet: a journey resets on reload.

---

`ProjectForge.md` is the implementation contract for the coding agent. This README is for
the human reviewer.
"# Project-Forge" 
