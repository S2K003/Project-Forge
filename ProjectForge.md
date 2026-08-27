# ForgeOps — Claude Code Project Instructions

> **ForgeOps is an AI-assisted engineering control plane for modern .NET teams.**
>
> It turns requirements into governed, testable, reviewable and observable engineering work while keeping deterministic engineering evidence and human judgment above AI recommendations.
>
> **ForgeOps is also a deliberate exercise in zero-budget engineering.** Every AI capability runs on a self-hosted, local model — not a paid API — and every hosted component runs on a free tier. The constraint is not a limitation to apologize for. It is a design requirement that must be handled with the same rigor as any other production constraint.

This file is the **implementation contract for Claude Code**. Read it before changing code.

---

## 0. Mission

Build ForgeOps as a portfolio-quality, production-minded .NET application that demonstrates:

- strong C# and ASP.NET Core engineering
- modular monolith architecture
- deterministic architecture and quality analysis
- AI-assisted requirements and code review, powered by a **self-hosted local LLM (Ollama, `qwen3:8b`)** — no paid AI API in the default configuration
- explicit human-in-the-loop decisions
- GitHub and CI/CD integration
- security and threat awareness
- observable background processing
- a **zero-cost, free-tier cloud topology**: Vercel (or an equivalent static host) for the frontend, a free-tier application host for the API, and the developer's own machine as a durable AI bridge to Ollama
- graceful degradation whenever the local AI bridge is offline — the product must never assume the AI machine is always reachable
- two explicit, clearly labeled application modes: a **Live Mode** that requires and continuously verifies a real AI Bridge connection, and a **Demo Mode** that replays the full product journey end-to-end with seeded data and no live dependency at all
- a distinctive, premium, **hand-built glassmorphism engineering UI** — not a default AI-generated template aesthetic

The project should make a senior engineer think:

> "This developer understands not only how to build software, but how to control the risks around software built with AI — and how to engineer seriously under real budget constraints instead of using cost as an excuse for shortcuts."

### Core rule

```text
AI Recommendation
       ↓
Deterministic Evidence
       ↓
Human Judgment
       ↓
Engineering Decision
```

Never turn this into:

```text
AI → Automatic Decision
```

---

# 1. How Claude Code Must Work

## 1.1 Read before coding

Before implementing a feature:

1. Inspect the existing repository.
2. Read relevant project files.
3. Identify the current architectural boundary.
4. Identify existing abstractions before creating new ones.
5. Check tests related to the change.
6. Check configuration and deployment implications.
7. Implement the smallest coherent vertical slice.
8. Run the relevant tests and validation.
9. Update documentation when the behavior or architecture changes.

Do not blindly generate an entire subsystem from this document.

## 1.2 Work in vertical slices

Prefer:

```text
Domain
  ↓
Application
  ↓
Infrastructure
  ↓
API/UI
  ↓
Tests
  ↓
Telemetry
```

for one useful capability at a time.

Do not create large amounts of disconnected scaffolding.

## 1.3 Preserve a runnable application

After every major change:

- the solution should build
- the application should start
- migrations should remain coherent
- tests should remain meaningful
- the UI should remain usable
- configuration should fail gracefully

Do not leave the repository in a permanently broken intermediate state.

---

# 2. Non-Negotiable Engineering Principles

## 2.1 AI assists; humans decide

AI output is advisory.

Every meaningful AI recommendation must be distinguishable from:

- deterministic system evidence
- human decisions
- final system state

Important AI interactions must record:

- provider
- model
- model version when available
- prompt/context version
- request timestamp
- response
- structured output
- validation result
- confidence
- human decision
- human modification/reason

## 2.2 Deterministic tooling wins

Do not ask an LLM to do work a deterministic tool can do reliably.

Preferred examples:

| Problem | Preferred mechanism |
|---|---|
| C# analysis | Roslyn |
| Architecture rules | NetArchTest/custom analyzers |
| Formatting | `dotnet format` |
| Build | `dotnet build` |
| Unit tests | xUnit |
| Browser tests | Playwright |
| Coverage | Coverlet |
| Security scanning | deterministic scanners |
| Telemetry | OpenTelemetry |
| Dependency graph | static analysis |
| AI interpretation | AI provider |

AI may explain deterministic findings. It must not invent their evidence.

## 2.3 Avoid architecture theatre

Do not add technology because it looks impressive.

Do not introduce:

- microservices without a demonstrated requirement
- CQRS everywhere
- event sourcing without a real need
- Kubernetes for the sake of Kubernetes
- a message broker before durable asynchronous messaging is actually required
- excessive interfaces
- repositories that merely wrap EF Core
- speculative abstractions
- unnecessary packages

The default architecture is a **modular monolith**.

---

# 3. Product Definition

ForgeOps is an engineering governance and intelligence platform.

It is NOT:

- a ChatGPT clone
- an AI code editor
- autocomplete
- a generic project management system
- an autonomous deployment agent
- a generic application generator

The product focuses on:

1. Requirements
2. Specifications
3. Architecture
4. Engineering rules
5. Quality gates
6. Pull Requests
7. AI code review
8. Test intelligence
9. Change impact
10. Human decisions
11. Auditability
12. Observability

---

# 4. Primary User Journey

The main demo must tell one coherent story:

```text
Requirement
    ↓
AI Specification
    ↓
Human Review
    ↓
Architecture Analysis
    ↓
Implementation / PR
    ↓
Deterministic Quality Gates
    ↓
AI Review
    ↓
Human Decision
    ↓
Merge / Deployment
    ↓
Telemetry
    ↓
Engineering Health
```

The UI should make this journey visually obvious.

This exact journey is what **Demo Mode** replays end-to-end with seeded data (§9A.2, §30), and what **Live Mode** performs for real against the AI Bridge (§9A.1).

---

# 5. MVP Scope

Implement the MVP in this order.

## Phase 1 — Foundation

- solution structure
- configuration
- database
- authentication foundation
- logging
- OpenTelemetry foundation
- error handling
- health checks
- CI build

## Phase 2 — Project + Dashboard

- projects
- repositories
- dashboard
- engineering health score
- recent activity
- system status

## Phase 3 — Requirements Intelligence

- requirement creation
- AI specification generation
- structured AI output
- specification review
- approval/rejection
- AI interaction audit

## Phase 4 — Engineering Intelligence

- architecture rules
- architecture analysis
- findings
- evidence
- quality gates
- gate runs
- deterministic health scoring

## Phase 5 — GitHub

- repository connection
- PR ingestion
- changed files
- CI status
- webhook validation
- read-only integration first

## Phase 6 — AI Review

- PR context assembly
- AI review
- evidence references
- confidence
- severity
- human accept/reject
- audit trail

## Phase 7 — Demo + Cloud

- seeded CustomerHub demo
- deliberately broken scenario
- Demo Mode (§9A.2): seeded, no-live-dependency replay of the full journey
- Live Mode connection gate (§9A.1): continuous AI Bridge health polling, hysteresis, lock/unlock behavior
- Docker deployment
- free-tier deployment (Vercel + API host + managed Postgres, §7A)
- production configuration
- cloud health checks
- portfolio documentation

Do not start Phase 7 before the core application is coherent.

---

# 6. Recommended Architecture

Use a modular monolith.

```text
src/
├── ForgeOps.Web              # Blazor WebAssembly (standalone, static output) — deployed to Vercel
├── ForgeOps.Api
├── ForgeOps.Application
├── ForgeOps.Domain
├── ForgeOps.Infrastructure
├── ForgeOps.AI                # AI Gateway + IAiProvider + OllamaBridgeProvider
├── ForgeOps.GitHub
├── ForgeOps.Analysis
├── ForgeOps.Quality
└── ForgeOps.Observability

tests/
├── ForgeOps.UnitTests
├── ForgeOps.IntegrationTests
├── ForgeOps.ArchitectureTests
└── ForgeOps.EndToEndTests
```

`ForgeOps.Web` must build to static assets only (`dotnet publish` → `wwwroot`). It talks to `ForgeOps.Api` exclusively over HTTP/JSON (and SignalR for the events listed in §38). It must never be assumed to run on the same host, same machine, or same network as the API — treat it as a fully independent deployable, because on Vercel it is.

### Dependency direction

```text
Web
 ↓
Api
 ↓
Application
 ↓
Domain

Infrastructure ─────┐
AI ──────────────────┤
GitHub ──────────────┤→ Application abstractions
Analysis ────────────┤
Quality ─────────────┘
```

Domain must remain independent of:

- EF Core
- ASP.NET Core
- Blazor
- Ollama
- GitHub SDKs
- OpenTelemetry implementations
- cloud-specific SDKs

Infrastructure implementations may depend on external systems.

---

# 7. Cloud-Ready Architecture

## 7.1 Cloud target — zero-budget by default

ForgeOps must run entirely on **free tiers**, with no paid cloud spend required to demonstrate the full product. This is a real engineering constraint, not a placeholder — treat it with the same discipline as any production requirement.

Default topology:

```text
                              Internet
                                 │
                 ┌───────────────┴───────────────┐
                 ▼                                ▼
        ┌─────────────────┐              ┌──────────────────┐
        │  Vercel (static)  │              │  Free-tier host   │
        │  ForgeOps.Web      │──HTTPS/────▶│  ForgeOps.Api      │
        │  (Blazor WASM)     │  SignalR    │  (e.g. Render /    │
        └─────────────────┘              │   Fly.io / Azure   │
                                          │   free App Service)│
                                          └─────────┬─────────┘
                                                    │
                       ┌────────────────────────────┼───────────────────────────┐
                       ▼                             ▼                           ▼
              Managed free Postgres            AI Gateway                  GitHub API
              (Neon / Supabase free tier)           │
                                                     ▼
                                        ┌──────────────────────────┐
                                        │   Secure AI Bridge Tunnel  │
                                        │  (Cloudflare Tunnel/ngrok) │
                                        └──────────────┬────────────┘
                                                        ▼
                                          Developer's PC — Ollama
                                              qwen3:8b (local model)
```

The application must not hard-code any single hosting provider into domain or application logic. Every provider (frontend host, API host, database, AI provider, tunnel) is configuration, not code.

## 7.2 The AI Bridge — local Ollama as the AI provider

There is no paid AI API in the default configuration. The developer's own machine **is** the AI backend.

```text
ForgeOps.Web (Vercel)
   ↓ HTTPS
ForgeOps.Api (free-tier host)
   ↓
AI Gateway → IAiProvider → OllamaBridgeProvider
   ↓
Secure outbound tunnel (Cloudflare Tunnel / ngrok, authenticated)
   ↓
Ollama on the developer's PC
   ↓
qwen3:8b
```

Requirements specific to this bridge:

- The tunnel endpoint must require authentication (a shared bridge token or tunnel-native auth) — never expose a bare Ollama port to the public internet.
- The API must treat the bridge exactly like any other unreliable external dependency: bounded timeout, bounded retry, circuit breaker, and a fast, honest failure when the developer's PC is offline or asleep.
- The bridge's reachability is itself telemetry: track AI availability as an observable signal (e.g. `ai_bridge_up`), not just a boolean toggle buried in configuration.
- Document the exact tunnel setup (`docs/ai-architecture.md`) so a reviewer can stand up the same bridge in minutes: install Ollama, pull `qwen3:8b`, expose it through the tunnel, put the tunnel URL in configuration.

If, later, budget allows a hosted model provider, it must slot in as an alternative `IAiProvider` implementation with zero changes to any feature code — see §7.3 and §9.

## 7.3 Local versus cloud AI

Local / demo deployment (the default):

```text
ForgeOps → AI Gateway → OllamaBridgeProvider → Tunnel → Ollama (qwen3:8b)
```

A future, budget-permitting deployment may use:

```text
ForgeOps → AI Gateway → Configured Cloud Provider
```

The provider abstraction must support both without touching feature code. Ollama, reached through the AI Bridge, is the default and primary provider for this project — design for it first, not as an afterthought behind a cloud provider.

## 7.4 Optional future path — Azure

Azure remains a reasonable **future** target once/if budget allows (Web App or container host, Azure Database for PostgreSQL, Azure Monitor, Key Vault). Keep the application container-first and twelve-factor so that moving from the free-tier topology to Azure is a configuration change, not a rewrite. Do not build Azure-specific assumptions into Phases 1–6 — cloud portability is the point.

## 7.5 Twelve-factor-style configuration

Never hard-code:

- database credentials
- GitHub tokens
- AI keys
- model names
- cloud resource identifiers
- webhook secrets

Use configuration providers and environment variables.

Example:

```text
ConnectionStrings__ForgeOps
Ai__Provider
Ai__BaseUrl
Ai__Model
GitHub__ClientId
GitHub__ClientSecret
GitHub__WebhookSecret
```

Production secrets should be supplied through a managed secret mechanism: the chosen free-tier API host's environment/secret store (e.g. Render/Fly.io environment groups) and Vercel's environment variables for the frontend build. Azure Key Vault remains the target if/when the optional Azure path (§7.4) is adopted.

## 7.6 Container-first deployment

Provide:

- Dockerfile
- `.dockerignore`
- production configuration
- health endpoint
- readiness/liveness strategy
- non-root container where practical
- graceful shutdown
- structured logs to stdout/stderr

The application must be deployable without relying on a developer workstation.

## 7.7 Database

Prefer PostgreSQL on a free managed tier (Neon or Supabase) for the initial deployment — both offer a durable free Postgres instance suitable for a portfolio demo without a credit card commitment.

Requirements:

- EF Core migrations
- connection pooling
- indexes for common queries
- foreign keys
- constraints
- concurrency handling where needed
- audit timestamps

Do not run destructive schema operations automatically in production.

---

# 7A. Budget & Hosting Strategy

This is a real constraint of the project, not a footnote. Every decision in this document should be readable against this table.

| Concern | Default (free-tier) choice | Why |
|---|---|---|
| Frontend hosting | Vercel (or Cloudflare Pages / GitHub Pages) | Free static hosting, CDN, HTTPS, instant deploys from `ForgeOps.Web` build output |
| API hosting | Render / Fly.io free tier (or equivalent) | Free container/app hosting sufficient for a portfolio demo's traffic |
| Database | Neon or Supabase (free PostgreSQL) | Durable managed Postgres with no card commitment |
| AI inference | Ollama + `qwen3:8b` on the developer's own PC | Zero marginal cost per request; demonstrates provider abstraction under a real, non-trivial constraint |
| AI reachability | Cloudflare Tunnel / ngrok from the PC to the API | Keeps Ollama off the public internet while still reachable from the hosted API |
| CI/CD | GitHub Actions (free minutes for public/portfolio repos) | No additional cost, already the natural home for the GitHub integration in Phase 5 |
| Secrets | Vercel env vars + API host's env/secret store | No paid secret manager required at this stage |
| Observability | OpenTelemetry → a free-tier trace/log sink (e.g. the API host's built-in logs, or a free Grafana Cloud/Honeycomb tier) | Keeps the observability story real without a paid APM contract |

Rules that follow from this:

1. Every external dependency (frontend host, API host, database, AI bridge, CI) must be swappable through configuration, never hard-coded — because the specific free-tier vendor may need to change.
2. Design for the free tier's real limits (cold starts, sleep-on-idle, connection caps, request timeouts) rather than assuming always-on infrastructure. Document these limits where they affect UX (e.g. "first request after idle may take a few seconds — show a loading state, not a broken one").
3. The AI Bridge is the one component that is expected to be intermittently offline by design (§9.3). Every other component should behave like normal cloud infrastructure.
4. Never let "it's free" become an excuse for skipping the reliability work in §8 — free-tier and unreliable are not synonyms, and the portfolio story is precisely that this was built *properly* despite the budget.

---

# 8. Cloud Reliability Requirements

Cloud hosting is not complete merely because the app loads.

Implement:

### Health

```text
GET /health
GET /health/ready
```

The readiness check may verify required dependencies.

The liveness check should remain lightweight.

### Resilience

External integrations must handle:

- timeout
- transient failure
- rate limiting
- unavailable provider
- invalid response
- cancellation

Do not add retries to every request automatically.

Retries must be:

- bounded
- appropriate to the operation
- cancellation-aware
- safe for idempotent operations

### Background jobs

Never hold an HTTP request open for a long-running quality run or AI review.

Use:

```text
HTTP request
    ↓
Create Job
    ↓
Background Worker
    ↓
Execute
    ↓
Persist Result
    ↓
Notify UI
```

Start with a simple durable application design. Add a queue only when the workload requires it.

---

# 9. AI Architecture

All AI requests go through one gateway.

```text
Feature
  ↓
AI Gateway
  ↓
Prompt Manager
  ↓
IAiProvider
  ↓
Ollama / Cloud Provider
```

No feature may directly call Ollama.

## 9.1 Provider abstraction

Maintain an abstraction similar to:

```csharp
public interface IAiProvider
{
    Task<AiResponse<T>> GenerateAsync<T>(
        AiRequest request,
        CancellationToken cancellationToken = default);
}
```

Possible providers:

- `OllamaBridgeProvider` — the default. Talks to Ollama through the authenticated AI Bridge tunnel described in §7.2, not a direct local call, because in the deployed topology the API does not run on the same machine as Ollama.
- a future hosted/cloud provider, added only if budget allows
- mock provider, for tests and offline development

The provider implementation must remain replaceable. Feature code must depend only on `IAiProvider` and must never know whether the current provider is local, tunnelled, or cloud-hosted.

## 9.2 Structured output

Prefer schemas over free-form text.

Validate:

- required fields
- enums
- maximum lengths
- confidence range
- severity
- recommendation type
- evidence references

Invalid AI output must never silently enter the domain model.

## 9.3 AI availability is not optional in Live Mode

Earlier drafts of this document treated AI unavailability as something the whole app should quietly work around. That is no longer the rule for **Live Mode**. Live Mode exists specifically to show a real AI Bridge doing real work — if the bridge is down, the honest thing to do is say so clearly and stop, not pretend the product is fully functional with a missing core feature.

The full behavior — continuous connection checking, the connection gate, hysteresis, and reconnection — is specified in **§9A Application Modes: Live Mode vs Demo Mode**. In short:

- **Live Mode** requires a verified AI Bridge connection to use the app at all. Losing the connection locks the app behind a clear status screen.
- **Demo Mode** is the resilience mechanism instead: it never depends on the AI Bridge, so there is always a way to show the complete product story regardless of whether the developer's PC is online.

This replaces the previous "AI unavailable — deterministic capabilities remain operational" behavior for the AI Bridge specifically. General external-dependency resilience (GitHub API, database, background jobs) in §8 is unaffected — that guidance still applies to those dependencies as written.

Never fake AI results in Live Mode. Demo Mode is the one place simulated results are allowed, and only because it is unambiguously labeled as simulated (§9A.3).

---

# 9A. Application Modes — Live Mode vs Demo Mode

ForgeOps ships with exactly two modes. Every screen must make it obvious which one is active — via a persistent, unmissable mode badge in the app shell, not a subtle color change.

```text
┌───────────────────────────────────────────────┐
│ ForgeOps   ● LIVE — AI Bridge Connected         │
├───────────────────────────────────────────────┤   or
│ ForgeOps   ◆ DEMO MODE — Simulated Data         │
└───────────────────────────────────────────────┘
```

## 9A.1 Live Mode — connection is mandatory

Live Mode is the real product: real backend, real database, real AI Bridge call to `qwen3:8b`.

**Connection monitoring**

- `ForgeOps.Web` polls a lightweight endpoint (e.g. `GET /health/ai-bridge`) on an interval (suggested: every 5–10 seconds) for as long as Live Mode is active.
- The endpoint reflects the AI Gateway's live view of the bridge (§7.2), not a cached value — it should itself have a short timeout so a hung bridge doesn't hang the health check.
- Use hysteresis, not a single failed ping: require **N consecutive failures** (e.g. 2) before treating the connection as down, and **N consecutive successes** before treating it as restored. This avoids the app flapping open/closed on a single dropped packet.

**Connection gate**

- On initial load, if the bridge is not reachable, show a full-screen **Connection Gate** instead of the app shell: clear status ("AI Bridge Offline"), what that means, a retry action, and a prominent way to switch to Demo Mode.
- If the connection drops mid-session (after hysteresis confirms it), show the same gate as a blocking overlay over the current screen — do not silently fall back to a degraded dashboard. State is preserved underneath; the overlay lifts automatically once the connection is confirmed restored.
- While gated, no AI-dependent or non-AI action should be reachable — the app is intentionally unusable, by design, because Live Mode's entire premise is a working AI Bridge.
- The gate must always offer **"Switch to Demo Mode"** as an explicit, one-click way out. This is the resolution to "what does a reviewer see if my PC is off" — they see Demo Mode, not a broken app.

**Do not** silently retry forever in the background while showing a stale or partially-functional UI. Fail visibly, fail fast, recover automatically.

## 9A.2 Demo Mode — always works, no live dependency

Demo Mode exists so the complete ForgeOps story — the journey in §4, played out in full — can be shown at any time, from any machine, whether or not the developer's PC and Ollama happen to be online. Treat it as a first-class product surface, not a fallback screen.

Requirements:

- Demo Mode replays the CustomerHub scenario in §30 end-to-end: a simple sign-in screen (not real authentication — a demo-only entry point into the walkthrough), requirement creation, AI specification, human review, architecture analysis, PR, quality gates, the deliberately broken scenario (§31), AI review, human decision, merge, telemetry, and final engineering health.
- All data is seeded/fixture data bundled with the app — Demo Mode should require **no live backend AI call and ideally no dependency on the API host being awake**. Ship the fixture data with `ForgeOps.Web` (or a single fast fixture endpoint) so the demo is resilient to free-tier cold starts too, not just to the AI Bridge being offline.
- "AI" steps in Demo Mode play back pre-recorded, realistic AI Gateway output (the same shape validated by §9.2) with a brief simulated "thinking" state, so the interaction pattern matches Live Mode exactly. This output must never be presented as if it came from a live model call.
- Every AI-originated element in Demo Mode is labeled, e.g. a small "Simulated" tag on the AI Decision Ribbon — the one exception to "never fake AI results" (§9.3), because it is transparent about being a recording, not a live result.
- Demo Mode is reachable directly (its own route, e.g. `/demo`) and from the Live Mode connection gate. Switching between modes must be explicit and unambiguous — never inferred silently from connection state.
- Target the same **~5 minute** walkthrough duration as §30, since Demo Mode's script *is* that scenario.

## 9A.3 What this buys the demo

A mentor or reviewer can always see the full working product in Demo Mode, instantly, regardless of network or hardware state — and can also see Live Mode do the same thing for real when the AI Bridge happens to be up. The gap between the two should be small: same UI, same components, same journey — only the data source and the mode badge differ.

---

# 10. AI Safety

Treat all repository content as untrusted input.

Defend against:

- prompt injection
- malicious source comments
- secret leakage
- excessive context
- hallucinated findings
- unauthorized actions
- repository data exfiltration

Explicitly separate:

```text
SYSTEM INSTRUCTIONS
TRUSTED APPLICATION DATA
UNTRUSTED REPOSITORY CONTENT
```

Repository text must never become executable instructions.

## Forbidden autonomous actions

The MVP must never let AI autonomously:

- delete repositories
- delete databases
- deploy production
- merge PRs
- modify infrastructure
- execute arbitrary shell commands

Any future automation requires a separate authorization design.

---

# 11. Domain Concepts

Start with only business concepts that are actually needed:

```text
User
Project
Repository
Requirement
Specification
AcceptanceCriterion
ArchitectureRule
ArchitectureFinding
QualityGate
QualityGateRun
PullRequest
PullRequestFinding
AiInteraction
AiRecommendation
HumanDecision
TestScenario
ArchitectureDecisionRecord
Deployment
TelemetrySnapshot
AuditEvent
```

Do not create an entity because it sounds enterprise-like.

---

# 12. Engineering Rules

Rules must be configurable.

Example:

```text
ARCH-001
Application must not reference Infrastructure.

ARCH-002
Domain must not reference Presentation.

SEC-001
Secrets must not exist in source code.

SEC-002
Sensitive endpoints require authorization.

API-001
Public endpoints require API documentation.

TEST-001
Critical business logic requires tests.

OBS-001
Production requests must generate telemetry.

AI-001
AI-generated changes require human approval.
```

Every rule should contain:

```text
Id
Name
Description
Severity
Category
DetectionMethod
RemediationGuidance
Enabled
```

Deterministic rules should produce machine-readable evidence.

---

# 13. Quality Gate Engine

Pipeline:

```text
Build
  ↓
Format
  ↓
Static Analysis
  ↓
Unit Tests
  ↓
Integration Tests
  ↓
Architecture Tests
  ↓
Security Checks
  ↓
Coverage
  ↓
AI Review
  ↓
Final Quality Gate
```

Each gate produces:

```text
Name
Status
Duration
Evidence
Errors
Warnings
Artifacts
Timestamp
```

States:

```text
Pending
Running
Passed
Failed
Skipped
Cancelled
```

A critical deterministic failure must be able to block the final quality state.

AI must not override deterministic gates.

---

# 14. Engineering Health Score

The score must be deterministic.

Example weighting:

```text
Tests             25%
Architecture      20%
Security          20%
Code Quality      15%
Observability     10%
Delivery           5%
Documentation      5%
```

The exact weights may be configurable later.

AI may explain the score.

AI must not arbitrarily determine the score.

Every score must have a "Why?" view.

Example:

```text
Engineering Health
87

Why?

✓ 42 architecture rules passed
✓ No circular dependencies
✓ Security checks passed
✓ 384 / 391 tests passing
⚠ Coverage below target
✗ 1 architecture violation
```

---

# 15. "Why?" Is a First-Class UX Pattern

Important ForgeOps results must expose evidence.

Never show only:

```text
Architecture: 87
```

Prefer:

```text
Architecture
87

42 rules evaluated
41 passed
1 failed

ARCH-001
Application → Infrastructure dependency

Evidence
ForgeOps.Application/... → ForgeOps.Infrastructure/...
```

The user should be able to trace:

```text
Score
 ↓
Finding
 ↓
Evidence
 ↓
Source / gate output
 ↓
Decision
```

This is a key differentiator of ForgeOps.

---

# 16. GitHub Integration

Start read-only where possible.

Support:

- repositories
- issues
- pull requests
- commits
- changed files
- review comments
- CI status
- webhooks

Write operations require explicit authorization.

Never store raw GitHub credentials in the database.

Validate webhook authenticity before processing.

Never trust webhook payloads blindly.

---

# 17. Pull Request Intelligence

A PR screen should answer:

```text
What changed?
What could be affected?
What gates passed?
What failed?
What did AI notice?
What evidence supports it?
What requires human attention?
```

Example:

```text
PR #142
Implement customer loyalty

17 files changed

Quality
✓ Build
✓ Unit Tests
✓ Integration
✓ Architecture
✓ Security
⚠ Coverage
✓ AI Review

Overall Health
84
```

---

# 18. AI Code Review

AI review may analyze:

- correctness
- maintainability
- architecture
- security
- tests
- error handling
- performance
- complexity
- observability

Every finding must distinguish:

```text
Confirmed
Likely
Possible
Suggestion
```

A finding should contain:

```text
Severity
Confidence
Finding
Evidence
Recommendation
Classification
```

Example:

```text
CRITICAL

Potential duplicate payment processing.

Evidence:
PaymentWebhookHandler.cs:47

Recommendation:
Introduce idempotency protection.

AI confidence:
93%

Classification:
Likely
```

Never describe confidence as statistical probability unless the system actually has the evidence to justify that interpretation.

---

# 19. Change Impact Analysis

Build a deterministic dependency graph.

When files change:

```text
Changed
  ↓
Dependency graph
  ↓
Potentially affected components
  ↓
Recommended deterministic tests
  ↓
AI business-impact explanation
```

AI may explain impact.

The factual dependency relationship must come from deterministic analysis.

---

# 20. Audit Trail

Audit meaningful actions.

Example:

```text
28 Aug 2026 14:31
Alex generated architecture review.

28 Aug 2026 14:32
AI identified ARCH-001.

28 Aug 2026 14:34
Alex rejected recommendation.

Reason:
Complexity not justified at current scale.
```

Audit records should be append-oriented.

Do not make audit history casually editable.

---

# 21. Observability

Use OpenTelemetry.

Capture:

- request duration
- database duration
- AI request duration
- AI failures
- quality gate duration
- GitHub API latency/errors
- background job duration
- job failures

Useful metrics:

```text
forgeops_ai_requests_total
forgeops_ai_request_duration
forgeops_ai_request_failures_total
forgeops_quality_gate_duration
forgeops_quality_gate_failures_total
forgeops_github_api_errors_total
forgeops_background_jobs_total
```

Use correlation IDs/traces so an operator can follow:

```text
User Action
  ↓
API Request
  ↓
Background Job
  ↓
AI Request
  ↓
Quality Gates
  ↓
Database
```

---

# 22. Security Baseline

Implement:

- authentication
- authorization
- role-based access
- input validation
- output validation
- secure configuration
- secret isolation
- audit logging
- rate limiting where appropriate
- secure HTTP headers
- CSRF protection where applicable
- webhook signature validation
- least privilege

Roles:

```text
Administrator
Engineering Manager
Developer
Reviewer
ReadOnly
```

Authorization must be enforced server-side.

Never rely on hiding a button in the UI.

---

# 23. UI/UX Direction — Distinctive Glassmorphism

## 23.1 Design objective

The UI must NOT look like a generic:

- Claude Code dashboard
- AI SaaS template
- Tailwind admin template
- Bootstrap admin panel
- generic purple-gradient AI startup
- card-grid clone

This applies with extra force to **how the UI is actually built**: do not accept the first default an AI code assistant produces for "a glassmorphism dashboard." Left unguided, that default tends toward the same handful of tells — an indigo-to-violet gradient background, centered hero copy, `rounded-2xl` cards with `shadow-lg` and `backdrop-blur-md`, an off-the-shelf icon set used exactly as provided, and evenly-spaced three-column feature grids. Treat any output that matches that pattern as a first draft, not a finished component. Push past it:

- pick a **specific, opinionated color story** for ForgeOps (e.g. deep graphite/near-black atmosphere with one restrained accent, not a generic blue-purple gradient) and hold to it everywhere via the tokens in §23.3
- vary surface shape and density deliberately — not every panel is a centered rounded card with the same corner radius and the same shadow
- design the signature components in §23.6 (Health Orb, Decision Ribbon, Evidence Rail, Gate Timeline) as the visual anchors of the product, not afterthought decorations bolted onto a generic admin layout
- choose typography and spacing that reads as a dense engineering tool, not a marketing landing page
- when in doubt, ask: *"would this screen be recognizable as ForgeOps with the logo removed?"* — if the honest answer is no, it is still generic

Use **glassmorphism as an information architecture**, not as decoration.

The visual identity should feel like:

> **Mission control for software engineering.**

Professional, technical, calm, dense, premium.

## 23.2 Visual language

Use:

- translucent surfaces
- layered depth
- subtle backdrop blur
- thin luminous borders
- soft ambient gradients
- dark atmospheric backgrounds
- restrained accent colors
- strong typography
- compact engineering data
- purposeful glow
- clear status semantics

Avoid:

- excessive blur
- giant rounded cards
- rainbow gradients
- glowing everything
- neon text everywhere
- floating blobs behind every component
- unreadable low-contrast glass
- decorative animation with no purpose

Glass should reveal hierarchy.

## 23.3 Signature visual system

Build a reusable design system.

Suggested tokens:

```text
--bg-void
--bg-atmosphere
--glass-surface
--glass-surface-strong
--glass-border
--glass-highlight
--text-primary
--text-secondary
--text-muted
--accent-ai
--accent-success
--accent-warning
--accent-danger
--accent-info
--shadow-glass
--blur-sm
--blur-md
--blur-lg
--radius-sm
--radius-md
--radius-lg
```

Do not scatter raw colors throughout components.

## 23.4 Layered application shell

Prefer:

```text
┌────────────────────────────────────────────────────────────┐
│ ForgeOps          Project: CustomerHub     ● Operational    │
├──────────────┬─────────────────────────────────────────────┤
│              │                                             │
│ Navigation   │             Main Workspace                  │
│              │                                             │
│ Overview     │  ┌────────┐ ┌────────┐ ┌────────┐          │
│ Requirements │  │ Health │ │ Gates  │ │ AI     │          │
│ Architecture │  └────────┘ └────────┘ └────────┘          │
│ Quality      │                                             │
│ Pull Requests│  ┌──────────────────────────────────────┐   │
│ AI Activity  │  │ Evidence / Activity / Attention      │   │
│ Decisions    │  └──────────────────────────────────────┘   │
│              │                                             │
└──────────────┴─────────────────────────────────────────────┘
```

But make it visually distinctive rather than literally reproducing this ASCII layout.

## 23.5 Glass hierarchy

Use three surface levels:

### Level 1 — Atmosphere

The application background.

Subtle gradient/noise/grid.

### Level 2 — Glass

Primary navigation, panels, command surfaces.

Translucent with border and blur.

### Level 3 — Focused glass

Active findings, selected cards, drawers, dialogs.

Slightly stronger opacity and border contrast.

Do not nest five layers of glass.

## 23.6 Signature elements

Create a few recognizable ForgeOps components:

### Engineering Health Orb

A compact circular health visualization showing:

```text
87
HEALTH
```

with deterministic segments around the perimeter.

### AI Decision Ribbon

Clearly communicates:

```text
AI RECOMMENDATION
↓
HUMAN DECISION
```

Never confuse AI output with system state.

### Evidence Rail

A side panel that shows:

```text
Finding
  ↓
Evidence
  ↓
Rule
  ↓
Source
  ↓
Decision
```

### Quality Gate Timeline

A horizontal/vertical execution timeline:

```text
Build ──●
Tests ──●
Arch  ──●
Sec   ──●
AI    ──◐
Final ──○
```

### Attention Queue

A persistent surface for things requiring human attention.

This should be more useful than a generic notification bell.

---

# 24. Dashboard UX

The dashboard must immediately answer:

1. What is happening?
2. What is broken?
3. What requires human attention?
4. What did AI recommend?
5. What changed?
6. Is the project safe to ship?

Recommended composition:

```text
Header
  ↓
Health + Operational Status
  ↓
Human Attention Queue
  ↓
Quality / Architecture / Security / Delivery
  ↓
Activity + AI Decisions
  ↓
Recent Deployments / Telemetry
```

Do not force the user through five pages to understand project health.

---

# 25. Motion Design

Animation must communicate state.

Allowed:

- panel transitions
- quality gate progress
- subtle hover depth
- status pulse
- drawer transitions
- loading shimmer
- live job progress

Avoid:

- constant floating animations
- rotating decorative objects
- aggressive parallax
- animation on every card
- long transitions

Respect:

```css
prefers-reduced-motion
```

---

# 26. Accessibility

Glassmorphism must never compromise accessibility.

Requirements:

- keyboard navigation
- visible focus states
- semantic HTML
- sufficient contrast
- labels for controls
- accessible dialogs
- screen-reader-friendly status messages
- reduced-motion support
- no status conveyed by color alone

Do not use transparency to make text unreadable.

---

# 27. Responsive Design

Support:

- desktop
- laptop
- tablet
- mobile

On small screens:

- navigation collapses
- data tables become scrollable or stacked
- dense dashboards reorganize
- side evidence panels become drawers
- charts remain understandable

Do not simply shrink desktop layouts.

---

# 28. UI Implementation Rules

Prefer reusable components over duplicated markup.

Create a small design system for:

```text
GlassPanel
GlassButton
GlassInput
StatusBadge
HealthOrb
Metric
EvidencePanel
FindingCard
DecisionRibbon
GateTimeline
AttentionItem
ActivityItem
CommandBar
EmptyState
Skeleton
Toast
Modal
Drawer
```

Do not create a component for every `<div>`.

Keep component APIs simple.

---

# 29. UI Content Rules

Use realistic engineering content.

Bad:

```text
AI magic complete!
```

Good:

```text
AI review completed
3 findings require human review
```

Bad:

```text
Amazing 99% secure
```

Good:

```text
Security
18 checks
18 passed
0 critical findings
```

Avoid marketing language inside the product.

---

# 30. Demo Scenario — CustomerHub

This scenario is the canonical script for **Demo Mode** (§9A.2). It must also be runnable in Live Mode against a real seeded project, so the two modes tell the identical story.

Ship a demonstration project:

```text
CustomerHub
```

Requirement:

> Customers should receive loyalty points after successful purchases.

Demo:

```text
0. Sign in (Demo Mode: demo-only entry screen — Live Mode: real auth)
1. Create requirement
2. Generate specification
3. Review acceptance criteria
4. Approve specification
5. Analyze architecture
6. Create implementation branch
7. Open Pull Request
8. Run quality gates
9. Detect deliberately introduced problem
10. AI explains the problem
11. Human reviews recommendation
12. Developer fixes issue
13. Gates pass
14. Merge
15. Show telemetry
16. Show final engineering health
```

Target demo duration:

**~5 minutes**

The demo should be understandable without a long verbal explanation.

---

# 31. Deliberately Broken Demo

Provide an optional demo branch/state containing realistic issues:

```text
Missing authorization
Duplicate event processing
Missing integration test
Application → Infrastructure dependency
Missing telemetry
```

The purpose is to demonstrate that ForgeOps can detect failure, not just celebrate success.

Do not fake findings.

Where possible, the broken state must be genuinely detectable by the implemented rules.

---

# 32. API Design

Use resource-oriented REST APIs.

Examples:

```http
POST /api/projects
GET  /api/projects/{id}

POST /api/projects/{id}/requirements
GET  /api/requirements/{id}
POST /api/requirements/{id}/generate-specification

POST /api/specifications/{id}/approve
POST /api/specifications/{id}/reject

GET  /api/projects/{id}/architecture
POST /api/projects/{id}/architecture/analyze

GET  /api/projects/{id}/quality
POST /api/projects/{id}/quality/runs

GET  /api/pull-requests/{id}
POST /api/pull-requests/{id}/review

GET /api/ai/interactions/{id}
GET /api/projects/{id}/decisions
```

Use consistent problem-details responses.

Never expose internal exceptions.

---

# 33. Error Handling

Use centralized error handling.

Never expose:

- stack traces
- secrets
- connection strings
- AI credentials
- internal database details
- sensitive infrastructure details

Errors should be:

- structured
- logged
- correlated
- safe for clients

---

# 34. Database Rules

Use EF Core migrations.

Requirements:

- sensible indexes
- foreign keys
- unique constraints where appropriate
- concurrency handling
- timestamps
- explicit relationships

Avoid soft deletion unless the business requirement justifies it.

Do not prematurely optimize.

---

# 35. Testing Strategy

Tests should establish confidence.

## Unit tests

Test:

- domain behavior
- scoring
- deterministic analyzers
- validation
- state transitions

## Integration tests

Test:

- database
- APIs
- infrastructure
- AI gateway with mocks/fakes
- GitHub integration boundaries

## Architecture tests

Verify:

```text
Domain → Infrastructure = forbidden
Application → Web = forbidden
Infrastructure → Web = forbidden
AI implementation → Domain = forbidden
GitHub implementation → Domain = forbidden
```

## End-to-end tests

At minimum:

```text
Create project
Create requirement
Generate specification
Approve specification
Run quality gates
Review PR
Record human decision
View dashboard
```

Use Playwright for critical browser journeys.

---

# 36. Code Quality

Use:

- nullable reference types
- async/await correctly
- cancellation tokens for long-running operations
- dependency injection
- clear naming
- small focused methods
- meaningful abstractions
- immutable data where appropriate

Avoid:

- dead code
- magic strings
- giant service classes
- hidden side effects
- unnecessary comments
- duplicate business logic
- interface-for-interface's-sake design

Modern C# features are encouraged when they improve clarity.

Clarity wins over cleverness.

---

# 37. Background Processing

Long-running operations should be jobs.

Example:

```text
QualityRun
├── Id
├── ProjectId
├── Status
├── StartedAt
├── CompletedAt
└── Error
```

Use a simple `BackgroundService` or equivalent initially.

A durable queue may be introduced later if cloud scale or reliability requires it.

Do not add a broker merely for architecture points.

---

# 38. Real-Time UI

Use SignalR only where live updates genuinely improve the product.

Useful events:

```text
AI review completed
Quality gate completed
GitHub synchronization completed
Deployment status changed
```

The dashboard should update without requiring constant refresh for active jobs.

Do not use SignalR everywhere.

The AI Bridge connection check (§9A.1) is a separate, simpler concern: a lightweight polling loop against `/health/ai-bridge`, not a SignalR channel — it must keep working even to detect the very first connection, before any SignalR session would exist.

---

# 39. Configuration

Use strongly typed options.

Example:

```json
{
  "Ai": {
    "Provider": "OllamaBridge",
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen3:8b",
    "TimeoutSeconds": 60
  }
}
```

In the deployed environment, `Ai:BaseUrl` points at the AI Bridge tunnel URL (Cloudflare Tunnel/ngrok), not `localhost` — the API host and the Ollama machine are different machines. `ForgeOps.Web`'s configuration is limited to the public API base URL; it must never receive AI provider details, tunnel URLs, or bridge tokens — those stay server-side in `ForgeOps.Api`.

Never commit production secrets.

Use:

```text
Local development:
User Secrets / environment variables

Deployed (free-tier default):
Vercel environment variables (ForgeOps.Web build-time config)
API host's environment/secret store (ForgeOps.Api — DB connection string,
  GitHub credentials, AI Bridge URL + bridge token)

Optional future Azure path:
Managed configuration + Azure Key Vault
```

---

# 40. Documentation Deliverables

The repository must contain:

```text
README.md

docs/
├── architecture.md
├── ai-architecture.md       # includes the AI Bridge / tunnel setup, step by step
├── hosting.md               # the free-tier topology in §7A, and how to swap any piece of it
├── security.md
├── threat-model.md
├── development.md
├── deployment.md
├── testing.md
└── decisions/
```

README should communicate:

1. Problem
2. Product vision
3. Screenshots
4. Architecture
5. Demo
6. Technology
7. Local setup
8. AI setup (including how to stand up the Ollama + tunnel AI Bridge)
9. Cloud deployment (the free-tier topology, plus the optional Azure path)
10. Testing
11. Security
12. Architecture decisions
13. Trade-offs
14. Limitations
15. Roadmap

The README is for the **human reviewer**.

This file is for the **coding agent**.

Do not turn README into a copy of this instruction file.

---

# 41. Portfolio Presentation Requirements

The finished application should visibly demonstrate:

```text
C#
.NET
ASP.NET Core
Architecture
Testing
Security
GitHub
CI/CD
Observability
Cloud deployment
AI integration
Human-in-the-loop design
```

The portfolio story is:

> I can build modern .NET software.

Then:

> I understand architecture.

> I understand deterministic testing and quality gates.

> I understand security.

> I understand DevOps and cloud deployment.

> I understand observability.

> I understand AI limitations.

> I know when to use AI.

> I know when not to use AI.

> I can engineer a real, production-shaped system under a genuine budget constraint — a self-hosted model and free-tier infrastructure — without cutting corners on reliability, security, or architecture.

---

# 42. Portfolio Screenshot Strategy

Build the UI so these screenshots are strong enough to stand alone:

### Screenshot 1 — Command Dashboard

Shows:

- health score
- project status
- attention queue
- quality gates
- recent engineering activity

### Screenshot 2 — Architecture Evidence

Shows:

- architecture score
- failed rule
- dependency visualization
- evidence
- recommendation

### Screenshot 3 — PR Intelligence

Shows:

- changed files
- quality gates
- AI findings
- confidence
- human decisions

### Screenshot 4 — AI Audit

Shows:

- model
- prompt version
- recommendation
- human decision
- decision reason

### Screenshot 5 — Cloud Operations

Shows:

- deployment status
- latency
- AI latency
- background jobs
- telemetry

Do not build separate fake screens just for screenshots. These should be real product capabilities.

---

# 43. Definition of Done

A feature is complete only when appropriate items are satisfied:

```text
✓ Implemented
✓ Tested
✓ Architecture boundaries respected
✓ Security considered
✓ Error handling implemented
✓ Logging implemented
✓ Telemetry implemented where appropriate
✓ UI polished
✓ Accessibility considered
✓ Configuration documented
✓ Cloud implications considered
✓ No compiler warnings
✓ No unnecessary TODOs
✓ CI passes
```

---

# 44. Cloud Definition of Done

A cloud feature is not complete until:

```text
✓ Container builds
✓ Production configuration works on the chosen free-tier host
✓ Secrets are externalized (Vercel env vars / API host secret store)
✓ Health endpoint works
✓ Readiness behavior is sensible
✓ Database migration strategy is documented
✓ Logs are structured
✓ Telemetry is emitted, including AI Bridge reachability
✓ Graceful shutdown works
✓ AI Bridge unreachable (PC/tunnel offline) is handled as an expected state, not a crash
✓ External API failures are handled
✓ Free-tier limits (cold start, sleep-on-idle, timeouts) are accounted for in UX
✓ HTTPS is assumed
✓ No development-only credentials are deployed
```

---

# 45. AI Definition of Done

An AI feature is not complete until:

```text
✓ Provider abstraction exists (IAiProvider), independent of transport
✓ AI Bridge tunnel is authenticated — Ollama is never exposed directly
✓ Structured output is validated
✓ Prompt version is tracked
✓ Model is tracked (provider + "qwen3:8b" or successor)
✓ AI interaction is auditable
✓ Bridge-unreachable failure is handled distinctly from a model/timeout failure
✓ Live Mode connection gate locks the app on confirmed disconnect (§9A.1), not just a toast
✓ Demo Mode reproduces the same journey with clearly labeled simulated AI output (§9A.2)
✓ Timeout is handled
✓ Cancellation is supported
✓ Human decision is possible
✓ AI cannot bypass deterministic gates
✓ AI cannot execute arbitrary commands
```

---

# 46. UI Definition of Done

A UI feature is not complete until:

```text
✓ Uses ForgeOps design system
✓ Fits the glassmorphism visual language
✓ Has clear hierarchy
✓ Works at desktop width
✓ Works at smaller widths
✓ Has keyboard interaction
✓ Has visible focus state
✓ Meets contrast expectations
✓ Supports reduced motion
✓ Does not rely on color alone
✓ Does not look like a generic admin template
✓ Does not look like an unedited first-draft AI-generated layout (§23.1)
✓ Shows the active mode (Live/Demo) unmistakably (§9A)
✓ Shows useful engineering information
```

---

# 47. Decision Rules for Claude Code

When uncertain:

### Choose simplicity over abstraction.

### Choose deterministic evidence over AI interpretation.

### Choose explicit state over hidden behavior.

### Choose server-side authorization over UI assumptions.

### Choose configuration over hard-coded environment details.

### Choose testable components over clever components.

### Choose a boring reliable solution over an impressive fragile one.

### Choose a real feature over a visual gimmick.

### Choose cloud portability over cloud lock-in in core business logic.

### Choose graceful degradation over pretending dependencies are always available.

### Choose free-tier discipline over paid convenience — but never let "free" become an excuse for skipping reliability work.

### Choose a hand-built, opinionated design decision over the first generic layout an AI assistant offers.

### Choose an explicit, visible Live/Demo mode boundary over silently blending real and simulated data.

---

# 48. Change Management

Before adding a dependency, ask:

1. Does .NET already solve this?
2. Does an existing dependency solve it?
3. Does the dependency materially reduce complexity?
4. What is the operational cost?
5. Does it complicate cloud deployment?
6. Does it make testing harder?

If the answer is not compelling, do not add it.

Before adding an architectural pattern, document:

```text
Problem
Why the simpler approach fails
Chosen approach
Alternatives
Trade-off
```

Use an ADR when the decision has lasting architectural impact.

---

# 49. What Claude Code Must Never Do

Never:

- expose secrets
- commit credentials
- bypass authorization
- disable architecture tests just to make CI pass
- delete tests to hide failures
- weaken quality gates to obtain green builds
- claim an AI finding is deterministic without evidence
- invent test results
- invent telemetry
- invent GitHub state
- fabricate cloud deployment success
- add technology merely for portfolio appearance
- silently change domain behavior while refactoring
- make destructive production actions autonomous

If a task requires a risky operation, stop and make the risk explicit.

---

# 50. Implementation Priority

When there is a conflict between polish and correctness:

```text
1. Correctness
2. Security
3. Architecture
4. Tests
5. Observability
6. Reliability
7. UX clarity
8. Visual polish
9. Animation
```

The glassmorphism UI must make the engineering system clearer, not distract from it.

---

# 51. Final Product Standard

A senior .NET engineer should be able to inspect ForgeOps and conclude:

```text
This developer understands C#.

This developer understands ASP.NET Core.

This developer understands architecture.

This developer understands testing.

This developer understands security.

This developer understands GitHub and CI/CD.

This developer understands cloud deployment.

This developer understands observability.

This developer understands AI.

Most importantly...

This developer understands that
AI-generated code is not automatically good software.
```

That is the standard.

---

# 52. Final Engineering Principle

ForgeOps exists to demonstrate:

> **AI-assisted engineering can become faster without becoming careless.**

Build the product so that every major capability reinforces that idea.

The most important visual, architectural and behavioral relationship in the application is:

```text
┌─────────────────────┐
│    AI RECOMMENDS    │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│ DETERMINISTIC       │
│ EVIDENCE            │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│ HUMAN JUDGMENT      │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│ ENGINEERING         │
│ DECISION            │
└─────────────────────┘
```

This should be reflected in:

- the domain model
- APIs
- audit trail
- quality gates
- AI architecture
- UI
- cloud operations
- documentation
- portfolio presentation
