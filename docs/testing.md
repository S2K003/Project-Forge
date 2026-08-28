# Testing

```bash
dotnet test
```

## What is covered today (`ForgeOps.UnitTests`)

| Area | Tests |
|---|---|
| AI resilience | `CircuitBreaker` opens on N consecutive failures, resets after the window, half-opens |
| AI output validation | `SpecificationDraftValidator` — required fields, criterion count, duplicate ids |
| Sandbox safety gate | `BannedApiScanner` — clean domain code passes; `System.IO` / `System.Net` / `Process` / `DllImport` / `unsafe` are flagged |
| Forge pipeline (end to end, deterministic) | `ForgePipelineTests` — the shipped (refined) implementation compiles against the curated reference set, passes the banned-API audit, and **the sandbox subprocess actually runs** the canonical acceptance suite; every criterion maps to a passing test. A separate test feeds a deliberately non-idempotent implementation and asserts canonical **AC-2 fails** and `RequirementSatisfied` is false |
| Demo Mode integrity | `CustomerHubJourneyTests` / `WebComponentTests` — every `JourneyStepKind` present and ordered (incl. `Refine`), the recorded audit permits execution, the **first run surfaces an unmet criterion** and the **Refine step regenerates the artefact and closes every criterion** (`Refinement.AllCriteriaMet`), every recorded AI interaction is flagged `Simulated`, health weights sum to 1 |

The forge pipeline tests need no AI — they run in CI. If the sandbox executable is not
next to the test host, those two tests no-op rather than fail.

## Deferred (roadmap)

- `ForgeOps.IntegrationTests` — API endpoints, EF Core, GitHub boundaries (once persistence lands)
- `ForgeOps.ArchitectureTests` — NetArchTest rules for the dependency direction in §6
- `ForgeOps.EndToEndTests` — Playwright over the Demo Mode walkthrough and the Live Mode
  connection gate
