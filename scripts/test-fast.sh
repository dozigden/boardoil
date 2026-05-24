#!/usr/bin/env bash
set -uo pipefail

# Decision log:
# - This script is the fast lane only; keep it speed-oriented for local iteration.
# - Slow API tests do not run here. Full-fidelity coverage lives in scripts/test-full.sh.
# - Changed-area auto mode defaults unknown/non-code paths to no tests.
# - Run all selected suites and aggregate failures so one run gives full signal.
# - Use script-owned build + compiled test DLL execution (no direct dotnet test flow).

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_TEST_PROJECT="BoardOil.Api.Tests/BoardOil.Api.Tests.csproj"
SERVICES_TEST_PROJECT="BoardOil.Services.Tests/BoardOil.Services.Tests.csproj"
API_TEST_DLL="BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll"
SERVICES_TEST_DLL="BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll"
FAST_API_EXCLUDE_CLASS_FILTER="*IntegrationTests*"

mode="auto"
run_api=false
run_services=false
run_web=false
restore_completed=false

declare -a failed_suites=()

for arg in "$@"; do
  case "$arg" in
    --api-only|--services-only|--web-only|--backend-only|--full)
      mode="${arg#--}"
      ;;
    --help|-h)
      cat <<'USAGE'
Usage: scripts/test-fast.sh [--api-only|--services-only|--web-only|--backend-only|--full]

Default mode is auto (changed-area detection):
- BoardOil.Services/** or BoardOil.Services.Tests/** -> Services tests
- BoardOil.Api/** or BoardOil.Api.Tests/**           -> API tests (fast lane excludes slow integration classes)
- BoardOil.Web/**                       -> npm run check + npm test
- Shared backend (Contracts/Abstractions/Ef/Data/Mcp.Contracts) -> API + Services tests
- Tooling/workflow/global files         -> API + Services tests
- Unknown/non-code paths                -> no tests

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

record_suite_failure() {
  local suite_name="$1"
  failed_suites+=("$suite_name")
}

run_suite() {
  local suite_name="$1"
  shift

  if "$@"; then
    return 0
  fi

  local exit_code=$?
  echo "[test-fast] Suite failed: ${suite_name} (exit ${exit_code})"
  record_suite_failure "$suite_name"
  return "$exit_code"
}

restore_backend_once() {
  if $restore_completed; then
    return 0
  fi

  echo "[test-fast] Restoring backend solution (one-time fallback)"
  dotnet restore BoardOil.slnx --locked-mode -maxcpucount:1 -nodeReuse:false
  restore_completed=true
}

build_test_project_release() {
  local project_path="$1"

  if dotnet build "$project_path" --configuration Release --no-restore -maxcpucount:1 -nodeReuse:false; then
    return 0
  fi

  echo "[test-fast] Build without restore failed for ${project_path}; retrying after restore."
  restore_backend_once
  dotnet build "$project_path" --configuration Release --no-restore -maxcpucount:1 -nodeReuse:false
}

run_api_release_tests() {
  echo "[test-fast] Running API fast tests (Release, excludes slow integration classes)"
  build_test_project_release "$API_TEST_PROJECT"
  dotnet "$API_TEST_DLL" --filter-not-class "$FAST_API_EXCLUDE_CLASS_FILTER"
}

run_services_release_tests() {
  echo "[test-fast] Running Services fast tests (Release)"
  build_test_project_release "$SERVICES_TEST_PROJECT"
  dotnet "$SERVICES_TEST_DLL"
}

run_backend_release_tests() {
  run_suite "api-fast" run_api_release_tests
  run_suite "services-fast" run_services_release_tests
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
      run_suite "api-fast" run_api_release_tests
      ;;
    services-only)
      run_suite "services-fast" run_services_release_tests
      ;;
    web-only)
      run_suite "web-checks" run_web_checks
      ;;
    backend-only)
      run_backend_release_tests
      ;;
    full)
      run_suite "full-lane" "$ROOT_DIR/scripts/test-full.sh"
      ;;
    *)
      echo "Unsupported mode: $mode" >&2
      exit 2
      ;;
  esac

  if ((${#failed_suites[@]} > 0)); then
    echo "[test-fast] Failed suites:"
    printf '  %s\n' "${failed_suites[@]}"
    echo "[test-fast] Rerun using scripts/test-fast.sh mode flags or scripts/test-full.sh for full coverage."
    exit 1
  fi

  echo "[test-fast] Done"
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
while IFS= read -r changed_file; do
  printf '  %s\n' "$changed_file"
done <<< "$changed_files"

while IFS= read -r file; do
  [[ -z "$file" ]] && continue
  case "$file" in
    BoardOil.Web/*)
      run_web=true
      ;;

    BoardOil.Services/*)
      run_services=true
      ;;

    BoardOil.Services.Tests/*)
      run_services=true
      ;;

    BoardOil.Api/*|BoardOil.Api.Tests/*)
      run_api=true
      ;;

    BoardOil.Contracts/*|BoardOil.Abstractions/*|BoardOil.Ef/*|BoardOil.Data.Abstractions/*|BoardOil.Mcp.Contracts/*)
      run_api=true
      run_services=true
      ;;

    BoardOil.slnx|Directory.Build.props|Directory.Packages.props|global.json|NuGet.config|.github/workflows/*|scripts/*)
      run_api=true
      run_services=true
      ;;

    AGENTS.md|AGENTS/*)
      ;;

    *)
      # Unknown paths default to no tests in fast mode.
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
  run_suite "api-fast" run_api_release_tests
elif $run_services; then
  run_suite "services-fast" run_services_release_tests
fi

if $run_web; then
  run_suite "web-checks" run_web_checks
fi

if ((${#failed_suites[@]} > 0)); then
  echo "[test-fast] Failed suites:"
  printf '  %s\n' "${failed_suites[@]}"
  echo "[test-fast] Rerun using scripts/test-fast.sh mode flags or scripts/test-full.sh for full coverage."
  exit 1
fi

echo "[test-fast] Done"
