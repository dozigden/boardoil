#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=""
TAG=""
APPLY=0
PUSH=0
REMOTE="origin"

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/release-tag.sh --version <semver> [--tag <vX.Y.Z>] [--apply] [--push] [--remote <name>]

Behaviour:
  - Validates release prerequisites for the provided version.
  - Runs frontend release gate: cd BoardOil.Web && npm run verify-release
  - Dry-run by default: does not create a git tag unless --apply is provided.
  - Optional --push pushes the created tag to the selected remote.

Examples:
  ./scripts/release-tag.sh --version 1.0.0
  ./scripts/release-tag.sh --version 1.0.0 --apply
  ./scripts/release-tag.sh --version 1.0.0 --apply --push
USAGE
}

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required tool: $1" >&2
    exit 1
  fi
}

is_clean_tree() {
  local root="$1"
  git -C "$root" diff --quiet \
    && git -C "$root" diff --cached --quiet \
    && [ -z "$(git -C "$root" ls-files --others --exclude-standard)" ]
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      VERSION="${2:-}"
      shift 2
      ;;
    --tag)
      TAG="${2:-}"
      shift 2
      ;;
    --apply)
      APPLY=1
      shift
      ;;
    --push)
      PUSH=1
      shift
      ;;
    --remote)
      REMOTE="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$VERSION" ]]; then
  echo "--version is required." >&2
  usage
  exit 1
fi

if [[ "$PUSH" -eq 1 && "$APPLY" -ne 1 ]]; then
  echo "--push requires --apply." >&2
  exit 1
fi

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid version '$VERSION'. Expected semantic version (for example 1.0.0)." >&2
  exit 1
fi

if [[ -z "$TAG" ]]; then
  TAG="v$VERSION"
fi

EXPECTED_TAG="v$VERSION"
if [[ "$TAG" != "$EXPECTED_TAG" ]]; then
  echo "Tag/version mismatch. Expected '$EXPECTED_TAG' for version '$VERSION', got '$TAG'." >&2
  exit 1
fi

require_tool git
require_tool npm
require_tool node

if ! git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Repository root is not a git work tree: $REPO_ROOT" >&2
  exit 1
fi

CURRENT_BRANCH="$(git -C "$REPO_ROOT" branch --show-current)"
if [[ "$CURRENT_BRANCH" != "main" ]]; then
  echo "Release tagging is restricted to 'main'. Current branch: '$CURRENT_BRANCH'." >&2
  exit 1
fi

if ! is_clean_tree "$REPO_ROOT"; then
  echo "Working tree is not clean. Commit or stash changes before running release-tag." >&2
  exit 1
fi

PACKAGE_VERSION="$(node -e "const fs=require('fs');const p=JSON.parse(fs.readFileSync(process.argv[1], 'utf8'));process.stdout.write(String(p.version ?? ''));" "$REPO_ROOT/BoardOil.Web/package.json")"
if [[ "$PACKAGE_VERSION" != "$VERSION" ]]; then
  echo "BoardOil.Web/package.json version is '$PACKAGE_VERSION' but expected '$VERSION'." >&2
  exit 1
fi

echo "[release-tag] Running frontend release gate (npm run verify-release)"
(
  cd "$REPO_ROOT/BoardOil.Web"
  npm run verify-release
)

if ! is_clean_tree "$REPO_ROOT"; then
  echo "Working tree changed after verification. Resolve generated changes before tagging." >&2
  exit 1
fi

if git -C "$REPO_ROOT" rev-parse -q --verify "refs/tags/$TAG" >/dev/null 2>&1; then
  echo "Tag '$TAG' already exists locally." >&2
  exit 1
fi

if [[ "$APPLY" -ne 1 ]]; then
  echo "[release-tag] Dry run complete. All checks passed for $VERSION."
  echo "[release-tag] Re-run with --apply to create tag '$TAG'."
  exit 0
fi

echo "[release-tag] Creating annotated tag '$TAG'"
git -C "$REPO_ROOT" tag -a "$TAG" -m "Release $TAG"

if [[ "$PUSH" -eq 1 ]]; then
  echo "[release-tag] Pushing '$TAG' to '$REMOTE'"
  git -C "$REPO_ROOT" push "$REMOTE" "$TAG"
fi

echo "[release-tag] Done."
