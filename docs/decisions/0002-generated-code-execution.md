# ADR 0002 — Generating and executing implementation code

**Status:** accepted · **Date:** 2026-08-28 · **Supersedes part of:** ADR 0001 scope

## Context

The product owner asked ForgeOps to go beyond specification: from an approved
specification it should **generate the implementation and its tests, audit them, and
actually run the code** so the requirement is demonstrably satisfied — both as a recorded
offline demo and live in real time.

This bumps against `ProjectForge.md`:

- §3 — "It is NOT … a generic application generator."
- §10 — "The MVP must never let AI autonomously … execute arbitrary shell commands."
- §2.1 — "AI assists; humans decide."

## Decision

Build the pipeline, but keep every one of those principles intact by construction:

| Principle | How it is preserved |
|---|---|
| AI produces a *candidate*, not a decision | `CodeGenerator` returns `GeneratedImplementation`; nothing merges or ships automatically |
| Deterministic tooling gates the AI | `GeneratedCodeAuditor`: Roslyn compile + analyzers + `BannedApiScanner` + architecture checks. A banned-API finding or compile error **blocks execution** |
| A human decides before code runs | The journey's **Human decision** step (§8) gates the **Run** step (§9). The API exposes `/api/forge/execute` separately so a human approves the exact code they reviewed |
| No arbitrary shell / autonomous action | Generated code is a small library compiled against a **curated reference set** (no `System.IO`, `System.Net`, interop, `System.Runtime.Loader`). It is executed only as `[ForgeFact]` test methods, in a **separate short-lived process** (`ForgeOps.Forge.Sandbox`) with a wall-clock budget and process-tree kill |
| "AI-generated code is not automatically good software" (§51) | ForgeOps ships its **own** canonical acceptance suite (`GeneratedSources.CanonicalSuite`), authored deterministically from the criteria. A weak implementation passes the model's own tests but fails the canonical AC-2 duplicate-event test — the §31 bug, now genuinely detected by running the code |

## Sandbox — what it is and is not

**Layers of defence, in order:**

1. **Static banned-API scan** over the model's source — process, filesystem, network,
   native interop, reflection-emit, `unsafe`. Any hit → the audit fails, nothing runs.
2. **Curated compile references** — the generated code physically cannot resolve
   `System.IO.File`, `System.Net.*`, etc.
3. **Separate process** — `ForgeOps.Forge.Sandbox`, launched per run, loads the compiled
   assembly in a collectible `AssemblyLoadContext`, runs the tests, prints JSON, exits.
4. **Wall-clock budget** — internal watchdog + parent `Process.Kill(entireProcessTree)`.

**Not yet done (documented limits, slice-3 hardening):** hard memory caps / OS Job
Objects, seccomp/container isolation, per-run user accounts. The static gate + reference
restriction + timeout is a reasonable posture for a portfolio demo running trusted-ish
model output; it is **not** a hostile-code sandbox.

**Execution posture is configuration.** `CodeRunner:Enabled=false` (set it on any shared
free-tier host) makes the pipeline stop after the audit — generation and the deterministic
audit still run for real; only the sandboxed execution is withheld. The recommended
topology runs the runner on a machine the developer controls (their PC, or the AI-Bridge
PC next to Ollama).

## Reference fallback (honest, not a fake)

If the model's output still does not compile after the repair budget, ForgeOps substitutes
its own **reference implementation** (`GeneratedSources.ReferenceImplementation`) so the
walkthrough can finish. This is disclosed, not hidden:

- `GeneratedImplementation.Origin` = `ReferenceFallback`, and the UI shows a warning banner
  plus the model's rejected attempt and its compiler errors.
- The AI interaction's validation result records the substitution.
- The model's own tests are kept only if they compile against the reference; otherwise a
  minimal ForgeOps test is used.

This satisfies §49 ("never invent test results") — the acceptance run still executes real
code against the real canonical suite; it is simply ForgeOps' code, clearly labelled as
such. Disable with `allowReferenceFallback: false` to see the raw failure instead.

## Consequences

- Live Mode can genuinely turn an idea into running, acceptance-verified code. Verified
  against local `qwen3:8b`: spec → implementation (0 repair rounds) → clean audit →
  sandbox run → **6/6 canonical tests, all 5 acceptance criteria satisfied**, ~100s.
- The generation target is deliberately narrow (complete a few method bodies in a fixed,
  fully-scaffolded class) so an 8B local model succeeds reliably; a compile-error **repair
  loop** (max 3 rounds) plus the reference fallback absorb the rest.
- Demo Mode ships a recorded run of exactly this pipeline (`CustomerHubJourney`), so the
  story is always available offline.
- If the generation target broadens later, this ADR must be revisited — a larger surface
  needs stronger isolation than layers 1–4 provide.
