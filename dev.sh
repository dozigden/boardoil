#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export BoardOil__ExposeLan=true
exec dotnet run --project "$ROOT_DIR/BoardOil.Dev/BoardOil.Dev.csproj" -- "$@"
