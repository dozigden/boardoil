Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $rootDir

node scripts/test-full.mjs @args
exit $LASTEXITCODE
