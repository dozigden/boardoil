Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $rootDir

node scripts/test-fast.mjs @args
exit $LASTEXITCODE
