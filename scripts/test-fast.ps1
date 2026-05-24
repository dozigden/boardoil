param(
    [ValidateSet("auto", "api-only", "services-only", "web-only", "backend-only", "full")]
    [string]$Mode = "auto"
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
    Write-Host "[test-fast] Running backend tests (Release, targeted projects)"
    & dotnet test BoardOil.Api.Tests/BoardOil.Api.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
    & dotnet test BoardOil.Services.Tests/BoardOil.Services.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
}

function Invoke-ApiReleaseTests {
    Write-Host "[test-fast] Running API tests (Release, targeted project)"
    & dotnet test BoardOil.Api.Tests/BoardOil.Api.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
}

function Invoke-ServicesReleaseTests {
    Write-Host "[test-fast] Running Services tests (Release, targeted project)"
    & dotnet test BoardOil.Services.Tests/BoardOil.Services.Tests.csproj --configuration Release -maxcpucount:1 -nodeReuse:false
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
$staged = (& git diff --name-only --cached 2>$null)
$unstaged = (& git diff --name-only 2>$null)
$untracked = (& git ls-files --others --exclude-standard 2>$null)
$changedFiles = @($staged + $unstaged + $untracked | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)

if (-not $changedFiles -or $changedFiles.Count -eq 0) {
    Write-Host "[test-fast] No changed files found; nothing to run."
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
