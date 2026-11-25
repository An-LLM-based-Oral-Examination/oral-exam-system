param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$agentProject = Join-Path $projectRoot 'native\Oes.WindowsAgent\Oes.WindowsAgent.csproj'
$outputDir = Join-Path $projectRoot 'native\dist\Oes.WindowsAgent'

if (-not (Test-Path $agentProject)) {
  throw "Không tìm thấy project Windows Agent tại: $agentProject"
}

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
  throw 'Không tìm thấy .NET SDK (dotnet) trong PATH. Hãy cài .NET 8 SDK trước khi build Oes.WindowsAgent.'
}

if (Test-Path $outputDir) {
  Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$publishArgs = @(
  'publish',
  $agentProject,
  '-c', $Configuration,
  '-r', $Runtime,
  '-o', $outputDir,
  '--nologo'
)

if ($SelfContained) {
  $publishArgs += @('--self-contained', 'true', '-p:PublishSingleFile=true')
} else {
  $publishArgs += @('--self-contained', 'false')
}

Write-Host "Building Oes.WindowsAgent ($Configuration / $Runtime) -> $outputDir"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host 'Oes.WindowsAgent build completed successfully.'
