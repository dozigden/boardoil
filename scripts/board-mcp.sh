#!/usr/bin/env bash
set -euo pipefail

MCP_URL="${BOARDOIL_MCP_URL:-http://192.168.1.144:5000/mcp}"
TOKEN="${BOARDOIL_MCP_TOKEN:-}"
BOARD_ID=1

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/board-mcp.sh [global options] <command> [command options]

Global options:
  --mcp-url <url>     MCP endpoint URL (default: http://192.168.1.144:5000/mcp)
  --token <token>     Machine PAT token (or set BOARDOIL_MCP_TOKEN)
  --board-id <id>     Default board id for commands that need one (default: 1)
  -h, --help          Show this help

Commands:
  tools-list
  board-get
  board-cards
  card-move --card-id <id> --column-title <title>
  card-description-set --card-id <id> --description <text>
  call --tool <name> [--arguments-json <json>]

Examples:
  ./scripts/board-mcp.sh --token "$TOKEN" board-cards
  ./scripts/board-mcp.sh --token "$TOKEN" card-move --card-id 33 --column-title "In Progress"
USAGE
}

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required tool: $1" >&2
    exit 1
  fi
}

normalise_json() {
  local raw="$1"

  if printf '%s' "$raw" | jq -e . >/dev/null 2>&1; then
    printf '%s' "$raw"
    return 0
  fi

  local sse_json
  sse_json="$(printf '%s' "$raw" | sed -n 's/^data:[[:space:]]*//p' | tail -n 1)"
  if [ -n "$sse_json" ] && printf '%s' "$sse_json" | jq -e . >/dev/null 2>&1; then
    printf '%s' "$sse_json"
    return 0
  fi

  echo "Failed to parse MCP response as JSON." >&2
  printf '%s\n' "$raw" >&2
  return 1
}

post_mcp() {
  local payload="$1"
  local raw
  raw="$(
    curl -fsS -X POST "$MCP_URL" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Accept: application/json, text/event-stream" \
      -H "Content-Type: application/json" \
      -d "$payload"
  )"
  normalise_json "$raw"
}

ensure_token() {
  if [ -z "$TOKEN" ]; then
    echo "No token provided. Use --token or set BOARDOIL_MCP_TOKEN." >&2
    exit 1
  fi
}

require_tool curl
require_tool jq

while [ $# -gt 0 ]; do
  case "$1" in
    --mcp-url)
      MCP_URL="${2:-}"
      shift 2
      ;;
    --token)
      TOKEN="${2:-}"
      shift 2
      ;;
    --board-id)
      BOARD_ID="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      break
      ;;
  esac
done

if [ $# -eq 0 ]; then
  usage >&2
  exit 1
fi

ensure_token

COMMAND="$1"
shift

case "$COMMAND" in
  tools-list)
    payload='{"jsonrpc":"2.0","id":"tools-list","method":"tools/list","params":{}}'
    post_mcp "$payload" | jq
    ;;
  board-get)
    payload="$(jq -cn --argjson id "$BOARD_ID" '{jsonrpc:"2.0",id:"board-get",method:"tools/call",params:{name:"board.get",arguments:{id:$id}}}')"
    post_mcp "$payload" | jq
    ;;
  board-cards)
    payload="$(jq -cn --argjson id "$BOARD_ID" '{jsonrpc:"2.0",id:"board-cards",method:"tools/call",params:{name:"board.get",arguments:{id:$id}}}')"
    post_mcp "$payload" | jq -r '.result.structuredContent.data.columns[] | .title as $column | .cards[]? | "\($column)\t#\(.id)\t\(.title)"'
    ;;
  card-move)
    card_id=""
    column_title=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --card-id)
          card_id="${2:-}"
          shift 2
          ;;
        --column-title)
          column_title="${2:-}"
          shift 2
          ;;
        *)
          echo "Unknown card-move option: $1" >&2
          exit 1
          ;;
      esac
    done

    if [ -z "$card_id" ] || [ -z "$column_title" ]; then
      echo "card-move requires --card-id and --column-title." >&2
      exit 1
    fi

    payload="$(jq -cn --argjson boardId "$BOARD_ID" --argjson id "$card_id" --arg columnTitle "$column_title" '{jsonrpc:"2.0",id:"card-move",method:"tools/call",params:{name:"card.move_by_column_name",arguments:{boardId:$boardId,id:$id,columnTitle:$columnTitle}}}')"
    post_mcp "$payload" | jq
    ;;
  card-description-set)
    card_id=""
    description=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --card-id)
          card_id="${2:-}"
          shift 2
          ;;
        --description)
          description="${2:-}"
          shift 2
          ;;
        *)
          echo "Unknown card-description-set option: $1" >&2
          exit 1
          ;;
      esac
    done

    if [ -z "$card_id" ]; then
      echo "card-description-set requires --card-id." >&2
      exit 1
    fi

    board_payload="$(jq -cn --argjson id "$BOARD_ID" '{jsonrpc:"2.0",id:"card-description-board-get",method:"tools/call",params:{name:"board.get",arguments:{id:$id}}}')"
    board_response="$(post_mcp "$board_payload")"

    card_json="$(printf '%s' "$board_response" | jq -c --argjson cardId "$card_id" '.result.structuredContent.data.columns[]?.cards[]? | select(.id == $cardId)' | head -n 1)"
    if [ -z "$card_json" ]; then
      echo "Could not find card id $card_id on board $BOARD_ID." >&2
      printf '%s\n' "$board_response" | jq >&2
      exit 1
    fi

    title="$(printf '%s' "$card_json" | jq -r '.title')"
    tag_names_json="$(printf '%s' "$card_json" | jq -c '.tagNames // []')"

    payload="$(jq -cn --argjson boardId "$BOARD_ID" --argjson id "$card_id" --arg title "$title" --arg description "$description" --argjson tagNames "$tag_names_json" '{jsonrpc:"2.0",id:"card-description-set",method:"tools/call",params:{name:"card.update",arguments:{boardId:$boardId,id:$id,title:$title,description:$description,tagNames:$tagNames}}}')"
    post_mcp "$payload" | jq
    ;;
  call)
    tool_name=""
    arguments_json="{}"
    while [ $# -gt 0 ]; do
      case "$1" in
        --tool)
          tool_name="${2:-}"
          shift 2
          ;;
        --arguments-json)
          arguments_json="${2:-}"
          shift 2
          ;;
        *)
          echo "Unknown call option: $1" >&2
          exit 1
          ;;
      esac
    done

    if [ -z "$tool_name" ]; then
      echo "call requires --tool." >&2
      exit 1
    fi

    payload="$(jq -cn --arg tool "$tool_name" --argjson args "$arguments_json" '{jsonrpc:"2.0",id:"call-tool",method:"tools/call",params:{name:$tool,arguments:$args}}')"
    post_mcp "$payload" | jq
    ;;
  *)
    echo "Unknown command: $COMMAND" >&2
    usage >&2
    exit 1
    ;;
esac
