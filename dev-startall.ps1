Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProject = Join-Path $rootDir "BoardOil.Api/BoardOil.Api.csproj"
$webDir = Join-Path $rootDir "BoardOil.Web"
$devDataDir = Join-Path $rootDir ".data/dev"
$mainDbPath = Join-Path $devDataDir "boardoil.dev.db"

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Test-LocalPortInUse {
    param([int]$Port)

    try {
        $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop
        return $null -ne $listener
    }
    catch {
        return $false
    }
}

function Stop-ProcessListeningOnPort {
    param([int]$Port)

    $connections = @()
    try {
        $connections = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop)
    }
    catch {
        return
    }

    if (-not $connections -or $connections.Count -eq 0) {
        return
    }

    $pids = @($connections | Select-Object -ExpandProperty OwningProcess -Unique)
    foreach ($pidValue in $pids) {
        if ($pidValue -le 0) {
            continue
        }

        try {
            $proc = Get-Process -Id $pidValue -ErrorAction Stop
            Write-Host "Stopping process on port ${Port}: $($proc.ProcessName) (PID $pidValue)"
            Stop-Process -Id $pidValue -Force -ErrorAction Stop
        }
        catch {
            Write-Host "Warning: failed to stop process PID $pidValue on port $Port."
        }
    }
}

function Stop-ExistingBoardOilApiProcesses {
    $apiProcesses = @()
    try {
        $apiProcesses = @(Get-Process -Name "BoardOil.Api" -ErrorAction Stop)
    }
    catch {
        return
    }

    foreach ($apiProcess in $apiProcesses) {
        try {
            Write-Host "Stopping existing BoardOil.Api process (PID $($apiProcess.Id))"
            Stop-Process -Id $apiProcess.Id -Force -ErrorAction Stop
        }
        catch {
            Write-Host "Warning: failed to stop BoardOil.Api process PID $($apiProcess.Id)."
        }
    }
}

function Get-CurrentBranchName {
    try {
        $branchName = (& git -C $rootDir rev-parse --abbrev-ref HEAD 2>$null).Trim()
    }
    catch {
        $branchName = ""
    }

    if ([string]::IsNullOrWhiteSpace($branchName)) {
        return "unknown"
    }

    if ($branchName -eq "HEAD") {
        try {
            $shortSha = (& git -C $rootDir rev-parse --short HEAD 2>$null).Trim()
        }
        catch {
            $shortSha = ""
        }

        if (-not [string]::IsNullOrWhiteSpace($shortSha)) {
            return "detached-$shortSha"
        }

        return "detached"
    }

    return $branchName
}

function Sanitize-BranchNameForPath {
    param([string]$BranchName)
    $sanitized = [Regex]::Replace($BranchName, "[^A-Za-z0-9._-]+", "_")
    $sanitized = [Regex]::Replace($sanitized, "_+", "_")
    $sanitized = $sanitized.Trim("_")
    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        return "unknown"
    }
    return $sanitized
}

function Resolve-DevDatabasePath {
    $dbMode = if ($env:BOARDOIL_DEV_DB_MODE) { $env:BOARDOIL_DEV_DB_MODE } else { "branch" }
    $mainBranchName = if ($env:BOARDOIL_MAIN_BRANCH_NAME) { $env:BOARDOIL_MAIN_BRANCH_NAME } else { "main" }
    $currentBranchName = Get-CurrentBranchName

    if ($dbMode -eq "shared" -or $currentBranchName -eq $mainBranchName) {
        return $mainDbPath
    }

    $branchDirName = Sanitize-BranchNameForPath -BranchName $currentBranchName
    return (Join-Path $devDataDir "branches/$branchDirName/boardoil.dev.db")
}

function Seed-BranchDatabaseFromMainIfNeeded {
    param([string]$TargetDbPath)

    $seedFromMain = if ($env:BOARDOIL_DEV_DB_SEED_FROM_MAIN) { $env:BOARDOIL_DEV_DB_SEED_FROM_MAIN } else { "1" }

    if ($TargetDbPath -eq $mainDbPath) { return }
    if ($seedFromMain -ne "1") { return }
    if (Test-Path -LiteralPath $TargetDbPath) { return }
    if (-not (Test-Path -LiteralPath $mainDbPath)) { return }

    $targetDir = Split-Path -Parent $TargetDbPath
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    Write-Host "Seeding branch database from main database snapshot..."
    Copy-Item -LiteralPath $mainDbPath -Destination $TargetDbPath -Force
    foreach ($suffix in @("-wal", "-shm")) {
        $source = "$mainDbPath$suffix"
        $dest = "$TargetDbPath$suffix"
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $dest -Force
        }
    }
}

Require-Command -Name "dotnet"
Require-Command -Name "npm"
Require-Command -Name "git"

if (-not (Test-Path -LiteralPath (Join-Path $webDir "node_modules"))) {
    throw "$webDir\node_modules is missing. Run 'cd BoardOil.Web; npm ci' first."
}

New-Item -ItemType Directory -Path $devDataDir -Force | Out-Null
$devDbPath = Resolve-DevDatabasePath
$devDbDir = Split-Path -Parent $devDbPath
New-Item -ItemType Directory -Path $devDbDir -Force | Out-Null
Seed-BranchDatabaseFromMainIfNeeded -TargetDbPath $devDbPath

Write-Host "Using development database: $devDbPath"

# Stop existing dev listeners early so API binaries are not locked during build.
Stop-ExistingBoardOilApiProcesses
Start-Sleep -Milliseconds 300
if (Test-LocalPortInUse -Port 5000) {
    Stop-ProcessListeningOnPort -Port 5000
    Start-Sleep -Milliseconds 500
}
if (Test-LocalPortInUse -Port 5173) {
    Stop-ProcessListeningOnPort -Port 5173
    Start-Sleep -Milliseconds 300
}

Write-Host "Building API ..."
& dotnet build $apiProject -maxcpucount:1 -nodeReuse:false

$logsDir = Join-Path $rootDir ".data/logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
$apiOutLog = Join-Path $logsDir "api.out.log"
$apiErrLog = Join-Path $logsDir "api.err.log"
$webOutLog = Join-Path $logsDir "web.out.log"
$webErrLog = Join-Path $logsDir "web.err.log"

function Initialize-LogFilePath {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $Path
    }

    try {
        Clear-Content -LiteralPath $Path -ErrorAction Stop
        return $Path
    }
    catch {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $directory = Split-Path -Parent $Path
        $filename = [System.IO.Path]::GetFileNameWithoutExtension($Path)
        $extension = [System.IO.Path]::GetExtension($Path)
        return (Join-Path $directory "$filename.$timestamp$extension")
    }
}

$apiOutLog = Initialize-LogFilePath -Path $apiOutLog
$apiErrLog = Initialize-LogFilePath -Path $apiErrLog
$webOutLog = Initialize-LogFilePath -Path $webOutLog
$webErrLog = Initialize-LogFilePath -Path $webErrLog

$apiScriptPath = Join-Path $logsDir "run-api.ps1"
$webScriptPath = Join-Path $logsDir "run-web.ps1"

$apiScript = @"
Set-StrictMode -Version Latest
`$ErrorActionPreference = "Stop"
`$env:ASPNETCORE_ENVIRONMENT = "Development"
`$env:DOTNET_ENVIRONMENT = "Development"
`$env:ConnectionStrings__BoardOil = "Data Source=$devDbPath"
dotnet run --no-launch-profile --no-build --project "$apiProject" --urls "http://127.0.0.1:5000"
"@

$webScript = @"
Set-StrictMode -Version Latest
`$ErrorActionPreference = "Stop"
Set-Location -LiteralPath "$webDir"
npm run sync:third-party-licences
if (-not `$env:VITE_BO_VERSION) {
    `$env:VITE_BO_VERSION = node -p "require('./package.json').version"
}
npm exec vite -- --port 5173
"@

Set-Content -LiteralPath $apiScriptPath -Value $apiScript -Encoding UTF8
Set-Content -LiteralPath $webScriptPath -Value $webScript -Encoding UTF8

Write-Host "Starting API on http://127.0.0.1:5000 ..."
if (Test-LocalPortInUse -Port 5000) {
    Stop-ProcessListeningOnPort -Port 5000
    Start-Sleep -Milliseconds 500
}
$apiProcess = Start-Process -FilePath "powershell" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $apiScriptPath) -RedirectStandardOutput $apiOutLog -RedirectStandardError $apiErrLog -WindowStyle Hidden -PassThru

Write-Host "Starting frontend on http://localhost:5173 ..."
$webProcess = Start-Process -FilePath "powershell" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $webScriptPath) -RedirectStandardOutput $webOutLog -RedirectStandardError $webErrLog -WindowStyle Hidden -PassThru

Write-Host "Logs:"
Write-Host "  API stdout: $apiOutLog"
Write-Host "  API stderr: $apiErrLog"
Write-Host "  Web stdout: $webOutLog"
Write-Host "  Web stderr: $webErrLog"

try {
    while ($true) {
        Start-Sleep -Seconds 1
        $apiExited = $null -ne $apiProcess -and $apiProcess.HasExited
        $webExited = $null -ne $webProcess -and $webProcess.HasExited

        if ($apiExited -or $webExited) {
            if ($apiExited) {
                Write-Host "API process exited with code $($apiProcess.ExitCode)."
            }
            if ($webExited) {
                Write-Host "Web process exited with code $($webProcess.ExitCode)."
            }
            break
        }
    }
}
finally {
    foreach ($proc in @($apiProcess, $webProcess)) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
