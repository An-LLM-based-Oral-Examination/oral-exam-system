#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(3, 20)]
    [int] $Iterations = 3,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $ResultsDirectory = (Join-Path $PSScriptRoot '../../TestResults/integration'),
    [switch] $NoBuild,
    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$projectPath = Join-Path $PSScriptRoot '../tests/OralExamination.IntegrationTests/OralExamination.IntegrationTests.csproj'
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Integration test project not found: $projectPath"
}

# Never accept a database connection string: Testcontainers owns its disposable DB.
$dockerCommand = Get-Command docker -CommandType Application -ErrorAction Stop | Select-Object -First 1
$dockerStart = [System.Diagnostics.ProcessStartInfo]::new()
$dockerStart.FileName = $dockerCommand.Path
$dockerStart.Arguments = 'info --format "{{.ServerVersion}}"'
$dockerStart.UseShellExecute = $false
$dockerStart.CreateNoWindow = $true
$dockerStart.RedirectStandardOutput = $true
$dockerStart.RedirectStandardError = $true
$dockerProcess = [System.Diagnostics.Process]::new()
$dockerProcess.StartInfo = $dockerStart
try {
    [void] $dockerProcess.Start()
    $dockerOutput = $dockerProcess.StandardOutput.ReadToEndAsync()
    $dockerError = $dockerProcess.StandardError.ReadToEndAsync()
    if (-not $dockerProcess.WaitForExit(30000)) {
        $dockerProcess.Kill()
        throw 'Docker daemon readiness check exceeded 30 seconds.'
    }
    if ($dockerProcess.ExitCode -ne 0) {
        throw "Docker daemon is unavailable (exit $($dockerProcess.ExitCode)). Start Docker before running integration tests."
    }
    Write-Host "Docker daemon ready: $($dockerOutput.GetAwaiter().GetResult().Trim())"
    [void] $dockerError.GetAwaiter().GetResult()
}
finally {
    $dockerProcess.Dispose()
}

$runId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'), [Guid]::NewGuid().ToString('N')
$runDirectory = [System.IO.Path]::GetFullPath((Join-Path $ResultsDirectory $runId))
[void] (New-Item -ItemType Directory -Path $runDirectory)
$previousLogDirectory = [Environment]::GetEnvironmentVariable('OES_INTEGRATION_LOG_DIRECTORY', 'Process')

try {
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        $iterationDirectory = Join-Path $runDirectory "iteration-$iteration"
        [void] (New-Item -ItemType Directory -Path $iterationDirectory)
        $env:OES_INTEGRATION_LOG_DIRECTORY = $iterationDirectory
        $testArguments = @(
            'test', $projectPath,
            '--configuration', $Configuration,
            '--filter', 'Category=PostgresIntegration',
            '--logger', 'trx;LogFileName=integration.trx',
            '--logger', 'console;verbosity=normal',
            '--collect', 'XPlat Code Coverage',
            '--results-directory', $iterationDirectory,
            '--diag', (Join-Path $iterationDirectory 'vstest.log'),
            '--blame-hang-timeout', '5m',
            '--blame-hang-dump-type', 'none'
        )
        if ($NoBuild) { $testArguments += '--no-build' }
        if ($NoRestore) { $testArguments += '--no-restore' }

        Write-Host "PostgreSQL integration iteration $iteration/$Iterations; evidence: $iterationDirectory"
        & dotnet @testArguments 2>&1 | Tee-Object -FilePath (Join-Path $iterationDirectory 'console.log')
        $testExitCode = $LASTEXITCODE
        if ($testExitCode -ne 0) {
            throw "Integration iteration $iteration failed: dotnet test exited $testExitCode. See $iterationDirectory"
        }

        $trxPath = Join-Path $iterationDirectory 'integration.trx'
        if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
            throw "Iteration $iteration produced no TRX; refusing a zero-test pass."
        }
        [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
        $counters = $trx.SelectSingleNode('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
        if ($null -eq $counters) { throw "Iteration $iteration has no TRX test counters." }
        $total = [int] $counters.total
        $executed = [int] $counters.executed
        $passed = [int] $counters.passed
        $failed = [int] $counters.failed
        $skipped = [int] $counters.notExecuted
        if ($total -le 0 -or $executed -ne $total -or $passed -ne $total -or $failed -ne 0 -or $skipped -ne 0) {
            throw "Iteration $iteration failed the no-skip/no-zero gate: total=$total executed=$executed passed=$passed failed=$failed skipped=$skipped."
        }
        $coverageFiles = @(Get-ChildItem -LiteralPath $iterationDirectory -Filter 'coverage.cobertura.xml' -Recurse -File)
        if ($coverageFiles.Count -eq 0) {
            throw "Iteration $iteration produced no Cobertura coverage artifact."
        }
        Write-Host "PASS iteration $iteration/${Iterations}: $passed/$total tests, zero failures/skips."
    }
    Write-Host "PASS: all $Iterations complete integration iterations succeeded. Evidence: $runDirectory"
}
finally {
    [Environment]::SetEnvironmentVariable('OES_INTEGRATION_LOG_DIRECTORY', $previousLogDirectory, 'Process')
}
