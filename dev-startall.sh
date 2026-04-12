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

stop_stale_vite() {
  local port=5173

  if ! command -v ss >/dev/null 2>&1; then
    return
  fi

  local ss_output=""
  ss_output=$(ss -ltnp 2>/dev/null || true)
  if [[ -z "$ss_output" ]]; then
    return
  fi

  local pids=""
  pids=$(awk -v port=":$port" '
    $0 ~ port {
      while (match($0, /pid=[0-9]+/)) {
        pid=substr($0, RSTART+4, RLENGTH-4)
        print pid
        $0=substr($0, RSTART+RLENGTH)
      }
    }
  ' <<<"$ss_output" | sort -u)

  if [[ -z "$pids" ]]; then
    return
  fi

  while IFS= read -r pid; do
    if [[ -z "$pid" ]] || ! kill -0 "$pid" 2>/dev/null; then
      continue
    fi

    local args=""
    args=$(ps -p "$pid" -o args= 2>/dev/null || true)
    if [[ -z "$args" ]]; then
      continue
    fi

    if grep -qi "vite" <<<"$args"; then
      echo "Stopping existing Vite process on port $port (pid $pid)..."
      kill "$pid" 2>/dev/null || true
    fi
  done <<<"$pids"
}

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
stop_stale_vite
(
  cd "$WEB_DIR"
  npm run dev
) &
web_pid=$!

wait -n "$api_pid" "$web_pid"
