#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

mode="auto"

for arg in "$@"; do
  case "$arg" in
    --api-only|--services-only|--web-only|--backend-only|--full)
      mode="${arg#--}"
      ;;
    --help|-h)
      cat <<'USAGE'
Usage: scripts/test-fast.sh [--api-only|--services-only|--web-only|--backend-only|--full]

Default mode is auto (changed-area detection):
- BoardOil.Services/**                  -> Services tests
- BoardOil.Api/**                       -> API tests
- BoardOil.Web/**                       -> npm run check + npm test
- Shared backend (Contracts/Abstractions/Ef/Persistence) -> API + Services tests
- Tooling/workflow/global files         -> API + Services tests

Diff source:
- staged + unstaged working tree changes
- untracked files
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

run_api=false
run_services=false
run_web=false

run_backend_release_tests() {
  echo "[test-fast] Running backend tests (Release, targeted projects)"
  dotnet test BoardOil.Api.Tests/BoardOil.Api.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
  dotnet test BoardOil.Services.Tests/BoardOil.Services.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
}

run_api_release_tests() {
  echo "[test-fast] Running API tests (Release, targeted project)"
  dotnet test BoardOil.Api.Tests/BoardOil.Api.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
}

run_services_release_tests() {
  echo "[test-fast] Running Services tests (Release, targeted project)"
  dotnet test BoardOil.Services.Tests/BoardOil.Services.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
}

run_web_checks() {
  echo "[test-fast] Running web checks"
  (
    cd BoardOil.Web
    npm run check
    npm test
  )
}

if [[ "$mode" != "auto" ]]; then
  case "$mode" in
    api-only)
      run_api_release_tests
      ;;
    services-only)
      run_services_release_tests
      ;;
    web-only)
      run_web_checks
      ;;
    backend-only)
      run_backend_release_tests
      ;;
    full)
      "$ROOT_DIR/scripts/test-full.sh"
      ;;
    *)
      echo "Unsupported mode: $mode" >&2
      exit 2
      ;;
  esac
  exit 0
fi

staged="$(git diff --name-only --cached || true)"
unstaged="$(git diff --name-only || true)"
untracked="$(git ls-files --others --exclude-standard || true)"
changed_files="$(printf "%s\n%s\n%s\n" "$staged" "$unstaged" "$untracked" | awk 'NF' | sort -u)"

if [[ -z "$changed_files" ]]; then
  echo "[test-fast] No changed files found; nothing to run."
  exit 0
fi

echo "[test-fast] Changed files:"
printf '  %s\n' $changed_files

while IFS= read -r file; do
  [[ -z "$file" ]] && continue
  case "$file" in
    BoardOil.Web/*)
      run_web=true
      ;;

    BoardOil.Services/*)
      run_services=true
      ;;

    BoardOil.Api/*|BoardOil.Api.Tests/*)
      run_api=true
      ;;

    BoardOil.Contracts/*|BoardOil.Abstractions/*|BoardOil.Ef/*|BoardOil.Data.Abstractions/*)
      run_api=true
      run_services=true
      ;;

    BoardOil.Services.Tests/*)
      run_services=true
      ;;

    BoardOil.slnx|Directory.Build.props|Directory.Packages.props|global.json|NuGet.config|.github/workflows/*|scripts/*)
      run_api=true
      run_services=true
      ;;

    AGENTS.md|AGENTS/*)
      ;;

    *)
      run_api=true
      run_services=true
      ;;
  esac
done <<< "$changed_files"

if ! $run_api && ! $run_services && ! $run_web; then
  echo "[test-fast] No code/test-impacting changes detected; nothing to run."
  exit 0
fi

if $run_api && $run_services; then
  run_backend_release_tests
elif $run_api; then
  run_api_release_tests
elif $run_services; then
  run_services_release_tests
fi

if $run_web; then
  run_web_checks
fi

echo "[test-fast] Done"
