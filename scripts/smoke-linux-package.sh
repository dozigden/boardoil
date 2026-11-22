#!/usr/bin/env bash
set -euo pipefail

# Check a newly installed service, including startup while migrations run.
usage() {
  echo "Usage: $0 [--nightly <build-number> <full-commit>] [base-url]" >&2
  exit 2
}

expected_build=''
if [[ ${1:-} == --nightly ]]; then
  [[ ${2:-} =~ ^[1-9][0-9]*$ && ${3:-} =~ ^[0-9a-f]{40}$ ]] || usage
  expected_build=$2
  expected_commit=$3
  shift 3
fi
[[ $# -le 1 && ${1:-} != -* ]] || usage
url=${1:-http://127.0.0.1:5000}
cd /usr/lib/boardoil
systemctl is-enabled boardoil.service
systemctl is-active boardoil.service
curl -fsS --retry 15 --retry-delay 1 --retry-connrefused "$url/api/health" |
  jq -e '.success and .data.status == "ok"' >/dev/null
if [[ -n $expected_build ]]; then
  version_info=$(curl -fsS "$url/api/version")
  if ! jq -es --arg build "$expected_build" --arg commit "$expected_commit" '
    length == 1 and (.[0] | .success == true and .data.channel == "nightly"
      and .data.build == $build and .data.commit == $commit)
  ' <<< "$version_info" >/dev/null; then
    printf 'Expected nightly build %s at %s. /api/version returned %s\n' \
      "$expected_build" "$expected_commit" "$version_info" >&2
    exit 1
  fi
fi
curl -fsS "$url/" | cmp - wwwroot/index.html
entry=$(sed -nE 's/.*<script[^>]*src="(\/assets\/[^"]+\.js)".*/\1/p' wwwroot/index.html | head -1)
if [[ -z "$entry" ]]; then
  echo "The packaged UI has no JavaScript entry point." >&2
  exit 1
fi
curl -fsS "$url$entry" | cmp - "wwwroot$entry"
echo "Verified installed service, API health, UI shell, and JavaScript entry point."
