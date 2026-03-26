#!/usr/bin/env bash

set -euo pipefail

API_BASE="http://127.0.0.1:5000"
USERNAME=""
PASSWORD=""

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/seed-boardoil-demo-data.sh --username <name> --password <password> [--api-base <url>]

Options:
  --api-base   API base URL (default: http://127.0.0.1:5000)
  --username   Login username (required)
  --password   Login password (required)
  -h, --help   Show this help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-base)
      API_BASE="${2:-}"
      shift 2
      ;;
    --username)
      USERNAME="${2:-}"
      shift 2
      ;;
    --password)
      PASSWORD="${2:-}"
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

if [[ -z "$USERNAME" || -z "$PASSWORD" ]]; then
  echo "Both --username and --password are required." >&2
  usage
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "Missing dependency: curl" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Missing dependency: jq" >&2
  echo "Install with: sudo apt-get update && sudo apt-get install -y jq" >&2
  exit 1
fi

API_BASE="${API_BASE%/}"
CSRF_TOKEN=""
COOKIE_JAR="$(mktemp)"
trap 'rm -f "$COOKIE_JAR"' EXIT

log_info() {
  echo "$1"
}

api_request() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local include_csrf="${4:-1}"
  local url="${API_BASE}${path}"

  local -a curl_args=(
    --silent --show-error
    --cookie "$COOKIE_JAR"
    --cookie-jar "$COOKIE_JAR"
    --request "$method"
    --url "$url"
    --write-out '\n%{http_code}'
  )

  if [[ "$include_csrf" == "1" && -n "$CSRF_TOKEN" ]]; then
    curl_args+=(--header "X-BoardOil-CSRF: $CSRF_TOKEN")
  fi

  if [[ -n "$body" ]]; then
    curl_args+=(--header "Content-Type: application/json" --data "$body")
  fi

  local response http_status response_body
  response="$(curl "${curl_args[@]}")"
  http_status="${response##*$'\n'}"
  response_body="${response%$'\n'*}"

  if [[ ! "$http_status" =~ ^2 ]]; then
    local api_message
    api_message="$(jq -r '.message // empty' <<<"$response_body" 2>/dev/null || true)"

    if [[ -n "$api_message" ]]; then
      echo "$method $path failed: HTTP $http_status | API: $api_message" >&2
    else
      echo "$method $path failed: HTTP $http_status" >&2
      echo "$response_body" >&2
    fi
    exit 1
  fi

  echo "$response_body"
}

assert_success_envelope() {
  local context="$1"
  local envelope="$2"

  if ! jq -e '.success == true' >/dev/null 2>&1 <<<"$envelope"; then
    local message
    message="$(jq -r '.message // "Unknown API error."' <<<"$envelope" 2>/dev/null || echo "Unknown API error.")"
    echo "$context failed: $message" >&2
    exit 1
  fi
}

extract_data() {
  local context="$1"
  local envelope="$2"

  assert_success_envelope "$context" "$envelope"
  jq -c '.data' <<<"$envelope"
}

cards_for_column() {
  local column="$1"

  case "$column" in
    "Ideas")
      cat <<'EOF_CARDS'
Adaptive card density control
Quick-add command palette
Story map swimlanes
Dependency visualiser
Bulk tag curator
Automation recipe builder
AI acceptance criteria helper
Retro board snapshot export
Release risk radar
Customer signal inbox
EOF_CARDS
      ;;
    "Ready")
      cat <<'EOF_CARDS'
Keyboard shortcut cheat sheet
Template cards for recurring work
Column WIP indicator
Drag ghost polish
Card age badge
Board empty-state rewrite
Tag legend panel
Copy card link action
Unsaved edit warning
Loading skeleton polish
EOF_CARDS
      ;;
    "In Progress")
      cat <<'EOF_CARDS'
Realtime typing stability hardening
Column virtual scrolling spike
Card description markdown parity
Tag editor accessibility audit
Session expiry toast messaging
Board reconnect banner
Column reorder edge-case fix
Optimistic update rollback guard
API error diagnostics panel
Mobile drag handle pass
EOF_CARDS
      ;;
    "Review")
      cat <<'EOF_CARDS'
Security header baseline
Audit log for card edits
Export board as JSON
CSV import mapping wizard
Colour contrast tune-up
New user onboarding checklist
API performance benchmark
Role-based menu visibility tests
Modal focus trap regression suite
Responsive spacing QA
EOF_CARDS
      ;;
    "Done")
      cat <<'EOF_CARDS'
Local font hosting for Montserrat
Primary and secondary button theme alignment
Header background and border refresh
Menu highlight consistency pass
Logo drop colour token wiring
Toolbar active and hover state refinement
Column manager action button cleanup
Nightly image workflow for changed builds
Workflow checks and validation pass
Board column scroll behaviour
EOF_CARDS
      ;;
    *)
      return 1
      ;;
  esac
}

default_tags_for_column() {
  local column="$1"

  case "$column" in
    "Ideas") printf '%s\n' "Discovery" "Product" "UX" ;;
    "Ready") printf '%s\n' "UX" "Accessibility" "Product" ;;
    "In Progress") printf '%s\n' "Frontend" "Backend" "API" ;;
    "Review") printf '%s\n' "Security" "Testing" "Docs" ;;
    "Done") printf '%s\n' "Release" "Ops" "Docs" ;;
    *) return 1 ;;
  esac
}

log_info "Signing in to $API_BASE as '$USERNAME'..."
login_body="$(jq -n --arg username "$USERNAME" --arg password "$PASSWORD" '{userName: $username, password: $password}')"
login_envelope="$(api_request POST "/api/auth/login" "$login_body" 0)"
login_data="$(extract_data "Login" "$login_envelope")"
CSRF_TOKEN="$(jq -r '.csrfToken // empty' <<<"$login_data")"

if [[ -z "$CSRF_TOKEN" ]]; then
  csrf_envelope="$(api_request GET "/api/auth/csrf" "" 0)"
  csrf_data="$(extract_data "Get CSRF token" "$csrf_envelope")"
  CSRF_TOKEN="$(jq -r '.csrfToken // empty' <<<"$csrf_data")"
fi

if [[ -z "$CSRF_TOKEN" ]]; then
  echo "Could not resolve CSRF token." >&2
  exit 1
fi

log_info "Loading current board..."
board_envelope="$(api_request GET "/api/board" "" 0)"
board_data="$(extract_data "Load board" "$board_envelope")"

mapfile -t existing_column_ids < <(jq -r '.columns[]?.id' <<<"$board_data")
if [[ ${#existing_column_ids[@]} -gt 0 ]]; then
  log_info "Removing ${#existing_column_ids[@]} existing columns (cards are removed by cascade)..."
  for column_id in "${existing_column_ids[@]}"; do
    delete_envelope="$(api_request DELETE "/api/columns/${column_id}" "" 1)"
    assert_success_envelope "Delete column $column_id" "$delete_envelope"
  done
else
  log_info "Board is already empty."
fi

seed_tags=(
  "Discovery"
  "Product"
  "UX"
  "Frontend"
  "Backend"
  "API"
  "Data"
  "Platform"
  "Security"
  "Testing"
  "Performance"
  "Accessibility"
  "Docs"
  "Ops"
  "Release"
)

log_info "Ensuring ${#seed_tags[@]} tags exist..."
for tag in "${seed_tags[@]}"; do
  tag_body="$(jq -n --arg name "$tag" '{name: $name}')"
  tag_envelope="$(api_request POST "/api/tags" "$tag_body" 1)"
  assert_success_envelope "Create tag $tag" "$tag_envelope"
done

columns=(
  "Ideas"
  "Ready"
  "In Progress"
  "Review"
  "Done"
)

extra_tags=("Data" "Platform" "Performance" "API" "Frontend" "Backend" "UX" "Testing" "Release")

declare -A created_column_ids

log_info "Creating ${#columns[@]} columns..."
for column in "${columns[@]}"; do
  column_body="$(jq -n --arg title "$column" '{title: $title}')"
  column_envelope="$(api_request POST "/api/columns" "$column_body" 1)"
  column_data="$(extract_data "Create column $column" "$column_envelope")"
  created_column_ids["$column"]="$(jq -r '.id' <<<"$column_data")"
done

total_cards=0
for column in "${columns[@]}"; do
  column_id="${created_column_ids[$column]}"

  mapfile -t titles < <(cards_for_column "$column")
  mapfile -t default_tags < <(default_tags_for_column "$column")

  log_info "Seeding ${#titles[@]} cards in '$column'..."

  for i in "${!titles[@]}"; do
    title="${titles[$i]}"
    extra_tag="${extra_tags[$((i % ${#extra_tags[@]}))]}"

    card_tag_names_json="$(
      {
        printf '%s\n' "${default_tags[@]}"
        printf '%s\n' "$extra_tag"
      } | awk 'NF && !seen[$0]++' | jq -R . | jq -s .
    )"

    description="Seed story for $column. $title. Add acceptance criteria, delivery notes, and links before scheduling."

    card_body="$(jq -n \
      --argjson boardColumnId "$column_id" \
      --arg title "$title" \
      --arg description "$description" \
      --argjson tagNames "$card_tag_names_json" \
      '{boardColumnId: $boardColumnId, title: $title, description: $description, tagNames: $tagNames}'
    )"

    card_envelope="$(api_request POST "/api/cards" "$card_body" 1)"
    assert_success_envelope "Create card '$title'" "$card_envelope"
    total_cards=$((total_cards + 1))
  done
done

echo
echo "Done."
echo "Columns created: ${#columns[@]}"
echo "Cards created:   $total_cards"
echo "Tags ensured:    ${#seed_tags[@]}"
echo "Note: Existing tags are kept (there is currently no tag delete endpoint)."
echo
echo "Example usage:"
echo "  ./scripts/seed-boardoil-demo-data.sh --api-base http://127.0.0.1:5000 --username admin --password 'Password1234!'"
