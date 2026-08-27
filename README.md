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

Sign in → create requirement → **AI specification** → human review → **architecture
analysis** (finds an `Application → Infrastructure` violation) → pull request → **quality
gates** (two blocking failures) → **AI review** (CRITICAL: duplicate points crediting) →
**human decision** → fix & merge → telemetry → **engineering health 87** with a full
"Why?".

The broken scenario is real: the gates and findings correspond to genuine rule
definitions, not fabricated results (ProjectForge.md §31).

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
ForgeOps.Web         Blazor WebAssembly (static)  ── deployed to Vercel
ForgeOps.Api         ASP.NET Core minimal API     ── deployed to a free-tier host
ForgeOps.AI          AI Gateway · IAiProvider · OllamaBridgeProvider · validation · telemetry
ForgeOps.Demo        CustomerHubJourney — single source of truth for both modes
ForgeOps.Contracts   shared DTOs / enums
```

- [docs/ai-architecture.md](docs/ai-architecture.md) — the AI Bridge & tunnel, step by step
- [docs/hosting.md](docs/hosting.md) — the free-tier topology and how to swap any piece
- [docs/decisions/0001-slice-1-scope.md](docs/decisions/0001-slice-1-scope.md) — what's in slice 1 and why

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

- Repository/requirement text is treated as **untrusted input**; system instructions,
  trusted context and untrusted content are kept in separate zones in every prompt.
- The AI Bridge tunnel is authenticated — Ollama is never exposed directly.
- AI cannot merge PRs, deploy, run commands, or override a deterministic gate.
- Invalid AI structured output never enters the model — it is rejected by a deterministic
  validator and recorded as such.

## Roadmap

1. **Slice 2** — `ForgeOps.Domain/Application/Infrastructure`, EF Core + free Postgres,
   persist journeys, auth foundation.
2. **Slice 3** — real deterministic analysers (`ForgeOps.Analysis`, `ForgeOps.Quality`)
   with Roslyn / NetArchTest, replacing the seeded architecture & gate fixtures.
3. **Slice 4** — GitHub read-only ingestion (repos, PRs, changed files, CI status,
   webhook validation).
4. **Slice 5** — live AI code review over a real PR diff; `ForgeOps.Observability`,
   OTLP export to a free sink; `IntegrationTests` / `EndToEndTests` (Playwright).
5. **Slice 6** — optional Azure path (Key Vault, Azure Database for PostgreSQL).

## Trade-offs & limitations (slice 1)

- Architecture findings, quality gates and PR data are **seeded fixtures** for the
  CustomerHub project — clearly framed as such in the UI — until slice 3.
- No persistence yet: a journey resets on reload.
- Live Mode wires the **specification** step to the real model; other steps use the
  seeded project data.

---

`ProjectForge.md` is the implementation contract for the coding agent. This README is for
the human reviewer.
