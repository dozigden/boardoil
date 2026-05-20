param(
    [switch]$BackendOnly,
    [switch]$WebOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $rootDir

$runBackend = -not $WebOnly
$runWeb = -not $BackendOnly

if ($BackendOnly -and $WebOnly) {
    throw "Choose only one of -BackendOnly or -WebOnly."
}

if ($runBackend) {
    Require-Command -Name "dotnet"
    Write-Host "[test-full] Backend: restore + build + tests"
    & dotnet restore BoardOil.slnx --locked-mode -maxcpucount:1 -nodeReuse:false
    & dotnet build BoardOil.slnx --configuration Release --no-restore -maxcpucount:1 -nodeReuse:false
    & dotnet BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll
    & dotnet BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll
}

if ($runWeb) {
    Require-Command -Name "npm"
    if (-not (Test-Path -LiteralPath (Join-Path $rootDir "BoardOil.Web/node_modules"))) {
        throw "BoardOil.Web\node_modules is missing. Run 'cd BoardOil.Web; npm install' first."
    }

    Write-Host "[test-full] Web: check + test"
    Push-Location (Join-Path $rootDir "BoardOil.Web")
    try {
        & npm run check
        & npm test
    }
    finally {
        Pop-Location
    }
}

Write-Host "[test-full] Done"
