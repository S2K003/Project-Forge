# ADR 0001 — Slice 1 scope: walkthrough-first, persistence-later

**Status:** accepted · **Date:** 2026-08-28

## Context

ProjectForge.md defines a large product across seven phases. The immediate goal is a
*working, deployable* application that (a) runs on Vercel, (b) always demonstrates the
full product story, and (c) proves the local-Ollama AI Bridge works when it is online.

## Decision

Slice 1 implements the **vertical journey experience** end to end, with the minimum
backend needed for Live Mode, and **defers persistence and the deep engineering
subsystems**.

In:

- `ForgeOps.Web` (Blazor WASM) — the full CustomerHub journey UI, glassmorphism design
  system, Live/Demo mode boundary, Connection Gate with hysteresis.
- `ForgeOps.Demo` — the journey as a compiled-in fixture (Demo Mode has zero runtime
  dependencies).
- `ForgeOps.AI` — AI Gateway, `IAiProvider`, `OllamaBridgeProvider`, circuit breaker,
  deterministic output validation, `forgeops_ai_bridge_up` telemetry.
- `ForgeOps.Api` — health endpoints, `/health/ai-bridge`, `/api/demo/journey`, real
  `generate-specification` via the AI Bridge.
- CI, Dockerfile, Vercel config, AI Bridge docs.

Deferred (documented in the README Roadmap):

- `ForgeOps.Domain` / `Application` / `Infrastructure`, EF Core + Postgres, auth,
  GitHub ingestion, the deterministic architecture/quality analysers,
  `ForgeOps.ArchitectureTests` / `IntegrationTests` / `EndToEndTests`.
- In slice 1 the architecture findings, quality gates and PR data shown in **both** modes
  are seeded fixtures for the CustomerHub project. They are clearly framed as such and
  are structurally identical to what the deferred analysers will produce.

## Why the simpler approach first

- The journey UI and the AI Bridge are the parts a reviewer cannot infer from a
  description — they need to be *seen working*.
- Demo Mode must never break; compiling the fixture in guarantees that regardless of
  network, free-tier cold starts, or the dev PC being asleep.
- Adding EF Core / a database before there is a feature that needs durable state would be
  scaffolding without a slice (ProjectForge.md §1.2).

## Consequences

- The "seeded fixture" framing must stay honest in the UI until the real analysers land.
- `CustomerHubJourney` is the single source of truth for both modes; a unit test guards
  that it covers every step kind.
