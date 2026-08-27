#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Vercel build for ForgeOps.Web (Blazor WebAssembly, static output).
# Vercel's build image has no .NET SDK, so we install it here, then publish
# the WASM app to artifacts/web/wwwroot (see outputDirectory in vercel.json).
# ---------------------------------------------------------------------------
set -euo pipefail

DOTNET_CHANNEL="10.0"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

echo "→ Installing .NET SDK ${DOTNET_CHANNEL}"
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel "${DOTNET_CHANNEL}" --install-dir "${HOME}/.dotnet"
export PATH="${HOME}/.dotnet:${PATH}"

dotnet --info | head -n 5

echo "→ Publishing ForgeOps.Web"
dotnet publish src/ForgeOps.Web/ForgeOps.Web.csproj -c Release -o artifacts/web

# If an API URL was provided as a Vercel env var, bake it into the published config
# so the deployed frontend can reach Live Mode. Empty = Demo Mode only.
if [ -n "${FORGEOPS_API_BASE_URL:-}" ]; then
  echo "→ Writing ApiBaseUrl into published appsettings.json"
  printf '{\n  "ForgeOps": { "ApiBaseUrl": "%s" }\n}\n' "${FORGEOPS_API_BASE_URL}" \
    > artifacts/web/wwwroot/appsettings.json
fi

echo "→ Done. Static site at artifacts/web/wwwroot"
