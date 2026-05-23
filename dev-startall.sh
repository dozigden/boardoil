#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/BoardOil.Api/BoardOil.Api.csproj"
WEB_DIR="$ROOT_DIR/BoardOil.Web"
DEV_DATA_DIR="$ROOT_DIR/.data/dev"
MAIN_DB_PATH="$DEV_DATA_DIR/boardoil.dev.db"

get_current_branch_name() {
  local branch_name=""
  branch_name=$(git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || true)
  if [[ -z "$branch_name" ]]; then
    echo "unknown"
    return
  fi

  if [[ "$branch_name" == "HEAD" ]]; then
    local short_sha=""
    short_sha=$(git -C "$ROOT_DIR" rev-parse --short HEAD 2>/dev/null || true)
    if [[ -n "$short_sha" ]]; then
      echo "detached-$short_sha"
      return
    fi

    echo "detached"
    return
  fi

  echo "$branch_name"
}

sanitise_branch_name_for_path() {
  local branch_name="$1"
  local sanitised=""
  sanitised=$(printf '%s' "$branch_name" | sed -E 's#[^A-Za-z0-9._-]+#_#g; s#_+#_#g; s#^_##; s#_$##')
  if [[ -z "$sanitised" ]]; then
    sanitised="unknown"
  fi
  echo "$sanitised"
}

copy_sqlite_database() {
  local source_db_path="$1"
  local destination_db_path="$2"

  if command -v sqlite3 >/dev/null 2>&1; then
    sqlite3 "$source_db_path" ".backup '$destination_db_path'"
    return
  fi

  cp "$source_db_path" "$destination_db_path"
  if [[ -f "${source_db_path}-wal" ]]; then
    cp "${source_db_path}-wal" "${destination_db_path}-wal"
  fi
  if [[ -f "${source_db_path}-shm" ]]; then
    cp "${source_db_path}-shm" "${destination_db_path}-shm"
  fi
}

resolve_dev_database_path() {
  local db_mode="${BOARDOIL_DEV_DB_MODE:-branch}"
  local main_branch_name="${BOARDOIL_MAIN_BRANCH_NAME:-main}"
  local current_branch_name=""
  current_branch_name=$(get_current_branch_name)

  if [[ "$db_mode" == "shared" ]]; then
    echo "$MAIN_DB_PATH"
    return
  fi

  if [[ "$current_branch_name" == "$main_branch_name" ]]; then
    echo "$MAIN_DB_PATH"
    return
  fi

  local branch_dir_name=""
  branch_dir_name=$(sanitise_branch_name_for_path "$current_branch_name")
  echo "$DEV_DATA_DIR/branches/$branch_dir_name/boardoil.dev.db"
}

seed_branch_database_from_main_if_needed() {
  local target_db_path="$1"
  local seed_from_main="${BOARDOIL_DEV_DB_SEED_FROM_MAIN:-1}"

  if [[ "$target_db_path" == "$MAIN_DB_PATH" ]]; then
    return
  fi

  if [[ "$seed_from_main" != "1" ]]; then
    return
  fi

  if [[ -f "$target_db_path" ]]; then
    return
  fi

  if [[ ! -f "$MAIN_DB_PATH" ]]; then
    return
  fi

  mkdir -p "$(dirname "$target_db_path")"
  echo "Seeding branch database from main database snapshot..."
  copy_sqlite_database "$MAIN_DB_PATH" "$target_db_path"
}

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
mkdir -p "$DEV_DATA_DIR"
DEV_DB_PATH="$(resolve_dev_database_path)"
mkdir -p "$(dirname "$DEV_DB_PATH")"
seed_branch_database_from_main_if_needed "$DEV_DB_PATH"
echo "Using development database: $DEV_DB_PATH"
echo "Building API ..."
dotnet_build_args=(
  -maxcpucount:1
  -nodeReuse:false
)

nuget_audit_enabled="${BOARDOIL_DEV_NUGET_AUDIT:-0}"
if [[ "$nuget_audit_enabled" != "1" ]]; then
  dotnet_build_args+=(-p:NuGetAudit=false)
fi

dotnet build "$API_PROJECT" "${dotnet_build_args[@]}"
ASPNETCORE_ENVIRONMENT=Development \
DOTNET_ENVIRONMENT=Development \
ConnectionStrings__BoardOil="Data Source=$DEV_DB_PATH" \
dotnet run --no-launch-profile --no-build --project "$API_PROJECT" --urls http://127.0.0.1:5000 &
api_pid=$!

echo "Starting frontend on http://localhost:5173 ..."
stop_stale_vite
(
  cd "$WEB_DIR"
  npm run dev
) &
web_pid=$!

wait -n "$api_pid" "$web_pid"
