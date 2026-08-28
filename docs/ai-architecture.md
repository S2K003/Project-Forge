# AI Architecture & the AI Bridge

ForgeOps runs **no paid AI API** in its default configuration. The AI backend is a local
model running under [Ollama](https://ollama.com) on the developer's own PC, reached from
the hosted API through an authenticated tunnel — the **AI Bridge**.

```
ForgeOps.Web (Vercel, static)
   │  HTTPS / JSON
   ▼
ForgeOps.Api (free-tier host)
   │
   ▼
AI Gateway ──► IAiProvider ──► OllamaBridgeProvider
   │
   ▼  authenticated outbound tunnel (Cloudflare Tunnel / ngrok)
Ollama on the developer's PC ──► local model (Ai:Model)
```

## Which model

`Ai:Model` is configuration, not code — swap it freely (§7.3, §7.5).

| Model | ~Size | Notes |
|---|---|---|
| **`qwen2.5-coder:7b`** *(default)* | 4.7 GB | Best small coding model; fits a 6 GB GPU. Clearly better at generated HTML/CSS/JS and C# than a general model. |
| `qwen3:8b` | 5.2 GB | The original choice — a general reasoning model, fine for spec/review, weaker at raw code. |
| `qwen2.5-coder:14b` / `:32b` | 9 / 20 GB | Better still if you have the VRAM. |
| `deepseek-coder-v2:16b`, `qwen3-coder:30b`, `gpt-oss:20b` | 9–20 GB | Strong, need more memory / partial CPU offload. |

`OllamaBridgeProvider` sends Ollama's `think: false` only for reasoning models
(`qwen3` non-coder, `deepseek-r1`, `magistral`, `gpt-oss`, `qwq`); a plain coding model
rejects that parameter. Override with `Ai:DisableThinking` if auto-detection is wrong.

## Rules this design follows

| Rule | Where |
|---|---|
| No feature calls Ollama directly — everything goes through the AI Gateway | `ForgeOps.AI/AiGateway.cs` |
| The provider is swappable; feature code only sees `IAiProvider` | `ForgeOps.AI/AiProviderContracts.cs` |
| Ollama is never exposed to the public internet — the tunnel requires auth | this doc, `Ai:BridgeToken` |
| Bridge-unreachable is handled distinctly from a model/timeout error | `AiBridgeUnreachableException` vs `AiModelException` |
| Bounded timeout + circuit breaker; fast honest failure when the PC is offline | `OllamaBridgeProvider`, `CircuitBreaker` |
| Bridge reachability is telemetry, not just a config flag | `forgeops_ai_bridge_up` metric, `AiBridgeStatusPoller` |
| AI structured output is validated deterministically before use | `SpecificationDraftValidator` |

## Stand up the bridge in ~5 minutes

### 1. Install Ollama and pull the model

```bash
# https://ollama.com/download
ollama pull qwen2.5-coder:7b
ollama serve            # serves http://localhost:11434
# sanity check:
curl http://localhost:11434/api/tags
```

### 2. Expose Ollama through a tunnel

**Option A — Cloudflare Tunnel (recommended, free, stable hostname):**

```bash
# one-time
cloudflared tunnel login
cloudflared tunnel create forgeops-bridge
cloudflared tunnel route dns forgeops-bridge bridge.example.com

# run (keep this running while you demo Live Mode)
cloudflared tunnel --url http://localhost:11434 run forgeops-bridge
```

Put an [Access policy / service token](https://developers.cloudflare.com/cloudflare-one/identity/service-tokens/)
in front of the hostname, or require the shared bridge token below.

**Option B — ngrok (fastest to try):**

```bash
ngrok http 11434 --request-header-add "authorization: Bearer $FORGEOPS_BRIDGE_TOKEN"
```

> ⚠️ Never expose `11434` without auth. The tunnel URL is effectively a public endpoint.

### 3. Point the API at the bridge

Set these on the API host (Render/Fly.io env vars) — **never** in `ForgeOps.Web`:

```
Ai__Provider     = OllamaBridge
Ai__BaseUrl      = https://bridge.example.com
Ai__Model        = qwen2.5-coder:7b
Ai__BridgeToken  = <shared secret, sent as: Authorization: Bearer ...>
Ai__TimeoutSeconds = 60
```

Locally, `src/ForgeOps.Api/appsettings.Development.json` already points at
`http://localhost:11434`, so `ollama serve` is all you need for local Live Mode.

### 4. Verify

```
GET  https://<api-host>/health/ai-bridge   → 200 { "up": true, "model": "qwen2.5-coder:7b" }
```

The frontend polls this every 7s in Live Mode. Two consecutive failures raise the
**Connection Gate**; two consecutive successes lift it (hysteresis, so a single dropped
packet doesn't flap the UI).

## What happens when the bridge is offline

- **Live Mode** — the app locks behind the Connection Gate with a clear status and a
  one-click **Switch to Demo Mode**. AI results are never faked here (§9.3).
- **Demo Mode** — unaffected. It has no dependency on the bridge, the API, or the
  network; the CustomerHub journey is compiled into the WASM bundle
  (`ForgeOps.Demo/CustomerHubJourney.cs`) and every AI step is a clearly-labelled
  recording.

## From specification to running code

Live Mode does not stop at a specification. Once a human approves the spec:

```
CodeGenerator (ForgeOps.AI)         the local model writes LoyaltyService + tests
   │   compile-error repair loop (max 2 rounds, errors fed back to the model)
   ▼
GeneratedCodeAuditor (ForgeOps.Forge)   Roslyn compile · analyzers ·
   │                                    BannedApiScanner · architecture checks
   ▼   (a banned-API finding or compile error blocks execution here)
human approves execution
   ▼
SandboxRunner → ForgeOps.Forge.Sandbox   separate process, curated references,
   │                                     wall-clock budget, process-tree kill
   ▼
canonical acceptance suite runs → results mapped to AC-1…AC-n
   │   (any AC unsatisfied, or a human wants a change)
   ▼
CodeGenerator.RefineImplementationAsync / RefineWebComponentAsync
   │   unmet criteria + failing checks + free-text feedback → model regenerates the artefact
   ▼   re-audit → re-run → new RefinementRound; a human still approves. Repeatable.
```

- Endpoints: `POST /api/forge/run` (generate + audit, `execute:false`),
  `POST /api/forge/execute` (audit + sandbox-run an already-generated implementation),
  `POST /api/forge/refine` (regenerate to close unmet criteria / apply feedback, then
  re-audit and re-run — returns a `ForgeResponse` whose `Result.Refinement` describes the
  round). Prompt versions `impl.refine.v1` / `webcomp.refine.v1`.
- Execution posture is config: `CodeRunner:Enabled=false` on a shared host stops after the
  audit. Full guardrails and their limits: [decisions/0002-generated-code-execution.md](decisions/0002-generated-code-execution.md).

## Prompt & model tracking

Every AI interaction records provider, model, model version, **prompt version**,
timestamp, latency, raw response, deterministic validation result, confidence, and the
human decision — see `AiInteractionRecord`. Prompt templates are code-reviewed constants
in `ForgeOps.AI/Prompts/PromptManager.cs`, versioned (`spec.v1`, …).
