param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$OrchestratorArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $rootDir "BoardOil.Dev/BoardOil.Dev.csproj"

& dotnet run --project $project -- @OrchestratorArguments
exit $LASTEXITCODE
