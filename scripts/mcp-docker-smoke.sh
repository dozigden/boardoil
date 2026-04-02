#!/usr/bin/env bash
set -euo pipefail

API_URL="http://localhost:5000"
MCP_URL="$API_URL/mcp"
ADMIN_USER="admin"
ADMIN_PASSWORD="Password1234!"
CARD_TITLE="mcp-smoke-$(date +%s)"
BOARD_ID=1
COOKIE_JAR="$(mktemp -t boardoil-smoke-cookies.XXXXXX)"

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required tool missing: $1" >&2
    exit 1
  fi
}

require_tool docker
require_tool curl
require_tool jq

normalise_mcp_json() {
  local raw="$1"
  if echo "$raw" | jq -e . >/dev/null 2>&1; then
    echo "$raw"
    return 0
  fi

  local sse_json
  sse_json=$(echo "$raw" | sed -n 's/^data:[[:space:]]*//p' | tail -n 1)
  if [ -z "$sse_json" ]; then
    echo "Could not parse MCP response as JSON or SSE" >&2
    echo "$raw" >&2
    return 1
  fi

  echo "$sse_json"
}

cleanup() {
  rm -f "$COOKIE_JAR" >/dev/null 2>&1 || true
  docker compose down --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "[smoke] Starting compose stack"
docker compose up --build -d

echo "[smoke] Waiting for API health"
for _ in $(seq 1 60); do
  status=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/api/health" || true)
  if [ "$status" = "200" ]; then
    break
  fi
  sleep 2
done
if [ "$status" != "200" ]; then
  echo "API did not become healthy" >&2
  exit 1
fi

echo "[smoke] Waiting for MCP auth gateway"
for _ in $(seq 1 60); do
  status=$(curl -s -o /dev/null -w "%{http_code}" -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":"ping","method":"tools/list","params":{}}' "$MCP_URL" || true)
  if [ "$status" = "401" ]; then
    break
  fi
  sleep 2
done
if [ "$status" != "401" ]; then
  echo "MCP endpoint did not become ready" >&2
  exit 1
fi

echo "[smoke] Bootstrapping initial admin"
register_status=$(curl -s -o /tmp/boardoil-register.json -w "%{http_code}" -X POST "$API_URL/api/auth/register-initial-admin" \
  -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -d "{\"userName\":\"$ADMIN_USER\",\"password\":\"$ADMIN_PASSWORD\"}")
if [ "$register_status" != "201" ] && [ "$register_status" != "409" ]; then
  echo "Initial admin bootstrap failed with status $register_status" >&2
  cat /tmp/boardoil-register.json >&2 || true
  exit 1
fi

csrf_token=$(jq -r '.data.csrfToken // empty' /tmp/boardoil-register.json)
if [ -z "$csrf_token" ] && [ "$register_status" = "409" ]; then
  echo "[smoke] Logging in as existing admin"
  login_payload=$(curl -fsS -X POST "$API_URL/api/auth/login" \
    -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
    -H "Content-Type: application/json" \
    -d "{\"userName\":\"$ADMIN_USER\",\"password\":\"$ADMIN_PASSWORD\"}")
  csrf_token=$(echo "$login_payload" | jq -r '.data.csrfToken // empty')
fi

if [ -z "$csrf_token" ]; then
  echo "Failed to obtain CSRF token for PAT creation" >&2
  exit 1
fi

echo "[smoke] Creating machine PAT"
create_pat_payload=$(curl -fsS -X POST "$API_URL/api/auth/machine/pats" \
  -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
  -H "X-BoardOil-CSRF: $csrf_token" \
  -H "Content-Type: application/json" \
  -d '{"name":"mcp-smoke-token","expiresInDays":30,"scopes":["mcp:read","mcp:write"],"boardAccessMode":"all","allowedBoardIds":[]}')
pat_token=$(echo "$create_pat_payload" | jq -r '.data.plainTextToken')
if [ -z "$pat_token" ] || [ "$pat_token" = "null" ]; then
  echo "Failed to create machine PAT" >&2
  echo "$create_pat_payload" >&2
  exit 1
fi

echo "[smoke] Calling MCP tools/list"
tools_list_payload=$(curl -fsS -X POST "$MCP_URL" \
  -H "Authorization: Bearer $pat_token" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list","params":{}}')
tools_list_payload=$(normalise_mcp_json "$tools_list_payload")

echo "$tools_list_payload" | jq -e '.result.tools[] | select(.name=="card.create")' >/dev/null

echo "[smoke] Reading board snapshot via board.get"
board_get_payload=$(curl -fsS -X POST "$MCP_URL" \
  -H "Authorization: Bearer $pat_token" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d "$(jq -cn --argjson boardId "$BOARD_ID" '{jsonrpc:"2.0",id:"board-get",method:"tools/call",params:{name:"board.get",arguments:{id:$boardId}}}')")
board_get_payload=$(normalise_mcp_json "$board_get_payload")
first_column_id=$(echo "$board_get_payload" | jq -r '.result.structuredContent.columns[0].id')
if [ -z "$first_column_id" ] || [ "$first_column_id" = "null" ]; then
  echo "Unable to determine first board column" >&2
  exit 1
fi

echo "[smoke] Creating card via card.create"
create_payload=$(jq -n \
  --argjson boardId "$BOARD_ID" \
  --argjson columnId "$first_column_id" \
  --arg title "$CARD_TITLE" \
  '{jsonrpc:"2.0",id:"card-create",method:"tools/call",params:{name:"card.create",arguments:{boardId:$boardId,columnId:$columnId,title:$title,description:"Created by MCP docker smoke",tagNames:[]}}}')

curl -fsS -X POST "$MCP_URL" \
  -H "Authorization: Bearer $pat_token" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d "$create_payload" >/dev/null

echo "[smoke] Verifying card exists via board.get"
board_verify_payload=$(curl -fsS -X POST "$MCP_URL" \
  -H "Authorization: Bearer $pat_token" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d "$(jq -cn --argjson boardId "$BOARD_ID" '{jsonrpc:"2.0",id:"board-verify",method:"tools/call",params:{name:"board.get",arguments:{id:$boardId}}}')")
board_verify_payload=$(normalise_mcp_json "$board_verify_payload")

echo "$board_verify_payload" | jq -e --arg title "$CARD_TITLE" '.result.structuredContent.columns[].cards[] | select(.title==$title)' >/dev/null

echo "[smoke] Docker MCP smoke test passed"
