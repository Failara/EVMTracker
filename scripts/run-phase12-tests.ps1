param(
    [ValidateSet("All", "Infrastructure", "Performance")]
    [string] $Mode = "All"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot "test/IntegrationTests/EVMTracker.EvmIndexer.IntegrationTests/EVMTracker.EvmIndexer.IntegrationTests.csproj"
$testFilter = switch ($Mode) {
    "Infrastructure" { "Category=ContainerIntegration" }
    "Performance" { "Category=PerformanceProbe" }
    default { "Category=ContainerIntegration|Category=PerformanceProbe" }
}

dotnet test $testProject `
    --configuration Release `
    --filter $testFilter `
    --logger "console;verbosity=normal"

if ($LASTEXITCODE -ne 0) {
    throw "Phase 12 test run failed with exit code $LASTEXITCODE."
}
