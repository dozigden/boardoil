#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/board-card-description-set-from-file.sh [global options] --card-id <id> --description-file <path>

Global options:
  --mcp-url <url>     MCP endpoint URL override
  --token <token>     Machine PAT token override
  --board-id <id>     Default board id override
  -h, --help          Show this help

Required options:
  --card-id <id>              Card id to update
  --description-file <path>   Path to UTF-8 markdown/text description file

Example:
  ./scripts/board-card-description-set-from-file.sh --card-id 38 --description-file /tmp/story38-description.md
USAGE
}

require_positive_integer() {
  local value="$1"
  local label="$2"

  if [[ ! "$value" =~ ^[0-9]+$ ]] || [ "$value" -le 0 ]; then
    echo "Invalid $label: '$value'. Must be a positive integer." >&2
    exit 1
  fi
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BOARD_MCP_SCRIPT="$SCRIPT_DIR/board-mcp.sh"

mcp_url=""
token=""
board_id=""
card_id=""
description_file=""

while [ $# -gt 0 ]; do
  case "$1" in
    --mcp-url)
      mcp_url="${2:-}"
      shift 2
      ;;
    --token)
      token="${2:-}"
      shift 2
      ;;
    --board-id)
      board_id="${2:-}"
      shift 2
      ;;
    --card-id)
      card_id="${2:-}"
      shift 2
      ;;
    --description-file)
      description_file="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [ -z "$card_id" ]; then
  echo "Missing required option: --card-id" >&2
  usage >&2
  exit 1
fi
require_positive_integer "$card_id" "--card-id"

if [ -z "$description_file" ]; then
  echo "Missing required option: --description-file" >&2
  usage >&2
  exit 1
fi

if [ ! -f "$description_file" ]; then
  echo "Description file not found: $description_file" >&2
  exit 1
fi

if [ ! -r "$description_file" ]; then
  echo "Description file is not readable: $description_file" >&2
  exit 1
fi

if [ -n "$board_id" ]; then
  require_positive_integer "$board_id" "--board-id"
fi

description="$(cat "$description_file")"

cmd=("$BOARD_MCP_SCRIPT")
if [ -n "$mcp_url" ]; then
  cmd+=(--mcp-url "$mcp_url")
fi
if [ -n "$token" ]; then
  cmd+=(--token "$token")
fi
if [ -n "$board_id" ]; then
  cmd+=(--board-id "$board_id")
fi

cmd+=(card-description-set --card-id "$card_id" --description "$description")
"${cmd[@]}"
