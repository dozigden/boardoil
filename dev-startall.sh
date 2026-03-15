#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/BoardOil.Api/BoardOil.Api.csproj"
WEB_DIR="$ROOT_DIR/BoardOil.Web"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet is required but not found on PATH." >&2
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "Error: npm is required but not found on PATH." >&2
  exit 1
fi

if [[ ! -d "$WEB_DIR/node_modules" ]]; then
  echo "Error: $WEB_DIR/node_modules is missing. Run 'cd BoardOil.Web && npm install' first." >&2
  exit 1
fi

api_pid=""
web_pid=""

cleanup() {
  trap - INT TERM EXIT

  if [[ -n "$api_pid" ]] && kill -0 "$api_pid" 2>/dev/null; then
    kill "$api_pid" 2>/dev/null || true
  fi

  if [[ -n "$web_pid" ]] && kill -0 "$web_pid" 2>/dev/null; then
    kill "$web_pid" 2>/dev/null || true
  fi

  wait "$api_pid" "$web_pid" 2>/dev/null || true
}

trap cleanup INT TERM EXIT

echo "Starting API on http://127.0.0.1:5000 ..."
ASPNETCORE_ENVIRONMENT=Development \
DOTNET_ENVIRONMENT=Development \
dotnet run --no-launch-profile --project "$API_PROJECT" --urls http://127.0.0.1:5000 &
api_pid=$!

echo "Starting frontend on http://localhost:5173 ..."
(
  cd "$WEB_DIR"
  npm run dev
) &
web_pid=$!

wait -n "$api_pid" "$web_pid"
