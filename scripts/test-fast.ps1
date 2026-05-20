param(
    [ValidateSet("auto", "api-only", "services-only", "web-only", "backend-only", "full")]
    [string]$Mode = "auto",
    [string]$Base = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-BackendReleaseTests {
    Write-Host "[test-fast] Running backend tests (Release, no-build)"
    & dotnet build BoardOil.slnx --configuration Release -maxcpucount:1 -nodeReuse:false
    & dotnet BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll
    & dotnet BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll
}

function Invoke-ApiReleaseTests {
    Write-Host "[test-fast] Running API tests (Release, no-build)"
    & dotnet build BoardOil.slnx --configuration Release -maxcpucount:1 -nodeReuse:false
    & dotnet BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll
}

function Invoke-ServicesReleaseTests {
    Write-Host "[test-fast] Running Services tests (Release, no-build)"
    & dotnet build BoardOil.slnx --configuration Release -maxcpucount:1 -nodeReuse:false
    & dotnet BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll
}

function Invoke-WebChecks {
    if (-not (Test-Path -LiteralPath (Join-Path $rootDir "BoardOil.Web/node_modules"))) {
        throw "BoardOil.Web\node_modules is missing. Run 'cd BoardOil.Web; npm install' first."
    }

    Write-Host "[test-fast] Running web checks"
    Push-Location (Join-Path $rootDir "BoardOil.Web")
    try {
        & npm run check
        & npm test
    }
    finally {
        Pop-Location
    }
}

$rootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $rootDir

Require-Command -Name "git"
Require-Command -Name "dotnet"
if ($Mode -eq "web-only" -or $Mode -eq "auto") {
    Require-Command -Name "npm"
}

if ($Mode -ne "auto") {
    switch ($Mode) {
        "api-only" { Invoke-ApiReleaseTests; exit 0 }
        "services-only" { Invoke-ServicesReleaseTests; exit 0 }
        "web-only" { Invoke-WebChecks; exit 0 }
        "backend-only" { Invoke-BackendReleaseTests; exit 0 }
        "full" {
            & (Join-Path $rootDir "scripts/test-full.ps1")
            exit 0
        }
    }
}

$changedFiles = @()

if (-not [string]::IsNullOrWhiteSpace($Base)) {
    $changedFiles = (& git diff --name-only "$Base...HEAD" 2>$null)
}
elseif ((& git rev-parse --verify origin/main 2>$null) -and $LASTEXITCODE -eq 0) {
    $changedFiles = (& git diff --name-only "origin/main...HEAD" 2>$null)
}
elseif ((& git rev-parse --verify main 2>$null) -and $LASTEXITCODE -eq 0) {
    $mergeBase = (& git merge-base main HEAD).Trim()
    if (-not [string]::IsNullOrWhiteSpace($mergeBase)) {
        $changedFiles = (& git diff --name-only "$mergeBase...HEAD" 2>$null)
    }
}

if (-not $changedFiles -or $changedFiles.Count -eq 0) {
    $staged = (& git diff --name-only --cached 2>$null)
    $unstaged = (& git diff --name-only 2>$null)
    $changedFiles = @($staged + $unstaged | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

if (-not $changedFiles -or $changedFiles.Count -eq 0) {
    Write-Host "[test-fast] No changed files found; running backend fast baseline."
    Invoke-BackendReleaseTests
    exit 0
}

Write-Host "[test-fast] Changed files:"
$changedFiles | ForEach-Object { Write-Host ("  " + $_) }

$runApi = $false
$runServices = $false
$runWeb = $false

foreach ($file in $changedFiles) {
    if ([string]::IsNullOrWhiteSpace($file)) { continue }

    switch -Wildcard ($file) {
        "BoardOil.Web/*" { $runWeb = $true; continue }
        "BoardOil.Services/*" { $runServices = $true; continue }
        "BoardOil.Api/*" { $runApi = $true; continue }
        "BoardOil.Api.Tests/*" { $runApi = $true; continue }
        "BoardOil.Contracts/*" { $runApi = $true; $runServices = $true; continue }
        "BoardOil.Abstractions/*" { $runApi = $true; $runServices = $true; continue }
        "BoardOil.Ef/*" { $runApi = $true; $runServices = $true; continue }
        "BoardOil.Data.Abstractions/*" { $runApi = $true; $runServices = $true; continue }
        "BoardOil.Services.Tests/*" { $runServices = $true; continue }
        "BoardOil.slnx" { $runApi = $true; $runServices = $true; continue }
        "Directory.Build.props" { $runApi = $true; $runServices = $true; continue }
        "Directory.Packages.props" { $runApi = $true; $runServices = $true; continue }
        "global.json" { $runApi = $true; $runServices = $true; continue }
        "NuGet.config" { $runApi = $true; $runServices = $true; continue }
        ".github/workflows/*" { $runApi = $true; $runServices = $true; continue }
        "scripts/*" { $runApi = $true; $runServices = $true; continue }
        "AGENTS.md" { continue }
        "AGENTS/*" { continue }
        default { $runApi = $true; $runServices = $true; continue }
    }
}

if (-not $runApi -and -not $runServices -and -not $runWeb) {
    Write-Host "[test-fast] No code/test-impacting changes detected; nothing to run."
    exit 0
}

if ($runApi -and $runServices) {
    Invoke-BackendReleaseTests
}
elseif ($runApi) {
    Invoke-ApiReleaseTests
}
elseif ($runServices) {
    Invoke-ServicesReleaseTests
}

if ($runWeb) {
    Invoke-WebChecks
}

Write-Host "[test-fast] Done"
