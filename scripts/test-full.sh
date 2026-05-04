#!/usr/bin/env bash
set -euo pipefail

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
  dotnet test BoardOil.Api.Tests/BoardOil.Api.Tests.csproj --configuration Release --no-build -maxcpucount:1 -nodeReuse:false
  dotnet test BoardOil.Services.Tests/BoardOil.Services.Tests.csproj --configuration Release --no-build -maxcpucount:1 -nodeReuse:false
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
