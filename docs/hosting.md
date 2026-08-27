# Hosting — zero-budget free-tier topology

Everything below runs on a free tier. No paid cloud spend is required to demonstrate the
full product (ProjectForge.md §7, §7A).

```
                         Internet
                            │
             ┌──────────────┴───────────────┐
             ▼                              ▼
   ┌───────────────────┐          ┌──────────────────────┐
   │ Vercel (static)   │  HTTPS   │ Free-tier API host   │
   │ ForgeOps.Web      │ ───────► │ ForgeOps.Api          │
   │ Blazor WASM       │  JSON    │ (Render / Fly.io)     │
   └───────────────────┘          └───────┬──────────────┘
                                          │
                   ┌──────────────────────┼─────────────────────┐
                   ▼                      ▼                     ▼
          Neon / Supabase          AI Bridge tunnel         GitHub API
          (free Postgres)          (Cloudflare / ngrok)
                                          ▼
                              Developer PC — Ollama qwen3:8b
```

| Concern | Default choice | Swap by changing |
|---|---|---|
| Frontend | Vercel (or Cloudflare Pages / GitHub Pages) | `vercel.json` + `deploy/vercel-build.sh` |
| API | Render / Fly.io free tier | container from `src/ForgeOps.Api/Dockerfile` |
| Database | Neon / Supabase free Postgres | `ConnectionStrings__ForgeOps` (not used yet — see Roadmap) |
| AI inference | Ollama + `qwen3:8b` on the dev PC | `Ai__Provider`, `Ai__BaseUrl` |
| AI reachability | Cloudflare Tunnel / ngrok | `Ai__BaseUrl`, `Ai__BridgeToken` |
| CI/CD | GitHub Actions | `.github/workflows/ci.yml` |
| Secrets | Vercel env vars + API host secret store | — |

No hosting provider name appears in domain or application code. Every provider is
configuration.

## Deploy the frontend (Vercel)

1. Import the repo in Vercel. Framework preset: **Other**.
2. Vercel reads `vercel.json`:
   - build command → `deploy/vercel-build.sh` (installs .NET 10, publishes the WASM app)
   - output directory → `artifacts/web/wwwroot`
3. Optional env var **`FORGEOPS_API_BASE_URL`** = `https://<your-api-host>`
   - set → the deployed site offers **Live Mode**
   - unset → the site is **Demo Mode only** (still fully functional)
4. Deploy. Demo Mode works immediately with no other setup.

## Deploy the API (Render / Fly.io)

```bash
docker build -f src/ForgeOps.Api/Dockerfile -t forgeops-api .
```

Environment variables on the API host:

```
ASPNETCORE_ENVIRONMENT = Production
Cors__AllowedOrigins__0 = https://<your-vercel-domain>
Ai__Provider   = OllamaBridge
Ai__BaseUrl    = https://<your-bridge-tunnel>
Ai__Model      = qwen3:8b
Ai__BridgeToken = <shared secret>
OTEL_EXPORTER_OTLP_ENDPOINT = <optional, a free trace sink>
```

Health endpoints: `/health` (liveness), `/health/ready` (readiness),
`/health/ai-bridge` (live bridge probe, bounded 4s).

## Designing for free-tier limits

- **Cold starts / sleep-on-idle** — the first request after idle can take a few seconds.
  The frontend shows loading states, and Demo Mode ships its fixture data compiled in so
  it does not even wait on the API.
- **The AI Bridge is expected to be intermittently offline by design.** Every other
  component behaves like normal always-on cloud infrastructure.
