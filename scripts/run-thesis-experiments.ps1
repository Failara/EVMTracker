[CmdletBinding()]
param(
    [ValidateRange(1, 100)]
    [int] $Repetitions = 5,

    [string] $OutputDirectory,

    [switch] $SkipWarmup
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot "test/IntegrationTests/EVMTracker.EvmIndexer.IntegrationTests/EVMTracker.EvmIndexer.IntegrationTests.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/thesis-experiments/$timestamp"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

function Invoke-CapturedCommand {
    param(
        [Parameter(Mandatory)]
        [string] $Command,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    try {
        (& $Command @Arguments 2>$null | Out-String).Trim()
    }
    catch {
        "unavailable"
    }
}

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$computer = Get-CimInstance Win32_ComputerSystem
$os = Get-CimInstance Win32_OperatingSystem
$gitRevision = Invoke-CapturedCommand -Command "git" -Arguments @("-c", "safe.directory=$repositoryRoot", "rev-parse", "HEAD")
$gitChanges = Invoke-CapturedCommand -Command "git" -Arguments @("-c", "safe.directory=$repositoryRoot", "status", "--porcelain")
$dockerVersion = Invoke-CapturedCommand -Command "docker" -Arguments @("version", "--format", "{{.Server.Version}}")
$dockerMemory = Invoke-CapturedCommand -Command "docker" -Arguments @("info", "--format", "{{.MemTotal}}")
$dotnetInfo = Invoke-CapturedCommand -Command "dotnet" -Arguments @("--info")

$metadata = [ordered]@{
    schemaVersion = 1
    capturedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    repository = [ordered]@{
        revision = $gitRevision
        dirty = -not [string]::IsNullOrWhiteSpace($gitChanges)
    }
    host = [ordered]@{
        cpu = $cpu.Name
        physicalCores = $cpu.NumberOfCores
        logicalProcessors = $cpu.NumberOfLogicalProcessors
        memoryBytes = [int64]$computer.TotalPhysicalMemory
        operatingSystem = $os.Caption
        operatingSystemVersion = $os.Version
    }
    software = [ordered]@{
        dockerServerVersion = $dockerVersion
        dockerAllocatedMemoryBytes = $dockerMemory
        dotnetInfo = $dotnetInfo
        dependencyImages = [ordered]@{
            postgresql = "postgres:18.6-alpine3.24"
            rabbitmq = "rabbitmq:4.2.9-management-alpine"
            anvil = "ghcr.io/foundry-rs/foundry:v1.7.1"
        }
    }
    protocol = [ordered]@{
        warmups = if ($SkipWarmup) { 0 } else { 1 }
        measuredRepetitions = $Repetitions
        randomizedCaseOrder = $true
    }
}

$metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resolvedOutput "environment.json") -Encoding utf8

$cases = @(
    [ordered]@{ Name = "broad-versus-filtered"; Filter = "FullyQualifiedName~FilteredModelExcludesIrrelevantLogsComparedWithBroadScan" },
    [ordered]@{ Name = "watchlist-scaling"; Filter = "FullyQualifiedName~FilteredLogQueryScalesAcrossTrackedAddressCounts" },
    [ordered]@{ Name = "block-range-scaling"; Filter = "FullyQualifiedName~FilteredQueryRecordsBlockRangeScaling" },
    [ordered]@{ Name = "confirmation-depth"; Filter = "FullyQualifiedName~ConfirmationDepthModelTests" },
    [ordered]@{ Name = "broker-backlog"; Filter = "FullyQualifiedName~RabbitMqBacklogPerformanceTests" }
)

function Invoke-ExperimentCase {
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Case,

        [Parameter(Mandatory)]
        [string] $RunName
    )

    Write-Host "Running $($Case.Name) ($RunName)..."
    dotnet test $testProject `
        --configuration Release `
        --filter $Case.Filter `
        --results-directory $resolvedOutput `
        --logger "trx;LogFileName=$RunName.trx" `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        throw "Experiment '$($Case.Name)' failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipWarmup) {
    foreach ($case in ($cases | Sort-Object { Get-Random })) {
        Invoke-ExperimentCase -Case $case -RunName "warmup-$($case.Name)"
    }
}

for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
    foreach ($case in ($cases | Sort-Object { Get-Random })) {
        $runName = "run-{0:D2}-{1}" -f $repetition, $case.Name
        Invoke-ExperimentCase -Case $case -RunName $runName
    }
}

Write-Host "Experiment artifacts: $resolvedOutput"
Write-Host "Keep every TRX file and calculate summaries according to docs/thesis/methodology.md."
