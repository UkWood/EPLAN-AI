param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\Audeu.EplanAi.LegacyHost.csproj'
$outputDir = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472'

if (-not (Test-Path $EplanApi29Dir)) {
    throw "EPLAN API directory not found: $EplanApi29Dir"
}

$required = @(
    'Eplan.EplApi.RemoteClientu.dll',
    'Eplan.EplApi.Remotingu.dll'
)
foreach ($name in $required) {
    $path = Join-Path $EplanApi29Dir $name
    if (-not (Test-Path $path)) {
        throw "Required EPLAN RemoteClient assembly not found: $path"
    }
}

Write-Host "Building Task 6 LegacyHost against real EPLAN RemoteClient API: $EplanApi29Dir"
& dotnet build $project -c Release "-p:EplanApi29Dir=$EplanApi29Dir"
if ($LASTEXITCODE -ne 0) {
    throw "Task 6 LegacyHost build failed with exit code $LASTEXITCODE."
}

$hostExe = Join-Path $outputDir 'Audeu.EplanAi.LegacyHost.exe'
if (-not (Test-Path $hostExe)) {
    throw "Build succeeded but LegacyHost executable was not found: $hostExe"
}

foreach ($name in $required) {
    $deployed = Join-Path $outputDir $name
    if (-not (Test-Path $deployed)) {
        throw "Build succeeded but required runtime assembly was not deployed beside LegacyHost: $deployed"
    }
}

Write-Host "PASS: Task 6 LegacyHost compiled against real EPLAN 2.9.4 RemoteClient assemblies: $hostExe"
Write-Host 'PASS: RemoteClient runtime assemblies were deployed beside LegacyHost.'
Write-Host 'Next: start EPLAN normally, open the safe test project, leave EPLAN running, then run scripts\test-task6-remoteclient.ps1.'
