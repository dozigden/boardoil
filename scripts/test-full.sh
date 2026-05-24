#!/usr/bin/env bash
set -euo pipefail

# Decision log:
# - This script is the full-fidelity test lane for local confidence before push.
# - Backend coverage here is authoritative (restore + build + full API + full Services).
# - scripts/test-fast.sh intentionally excludes slow tests and should delegate here for full runs.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run_backend=true
run_web=true

for arg in "$@"; do
  case "$arg" in
    --backend-only)
      run_web=false
      ;;
    --web-only)
      run_backend=false
      ;;
    --help|-h)
      cat <<'USAGE'
Usage: scripts/test-full.sh [--backend-only|--web-only]

Runs CI-like full checks locally:
- Backend: restore + release build + API tests + Services tests
- Web: npm run check + npm test
USAGE
      exit 0
      ;;
    *)
      echo "Unknown argument: $arg" >&2
      exit 2
      ;;
  esac

done

cd "$ROOT_DIR"

if $run_backend; then
  echo "[test-full] Backend: restore + build + tests"
  dotnet restore BoardOil.slnx --locked-mode -maxcpucount:1 -nodeReuse:false
  dotnet build BoardOil.slnx --configuration Release --no-restore -maxcpucount:1 -nodeReuse:false
  dotnet BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll
  dotnet BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll
fi

if $run_web; then
  echo "[test-full] Web: check + test"
  (
    cd BoardOil.Web
    npm run check
    npm test
  )
fi

echo "[test-full] Done"
