param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472'
$hostExe = Join-Path $outputDir 'Audeu.EplanAi.LegacyHost.exe'

if (-not (Test-Path $hostExe)) {
    throw "Task 6 LegacyHost executable not found: $hostExe. Run scripts\verify-task6-build.ps1 first."
}

$runningEplan = Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue
if (-not $runningEplan) {
    throw 'EPLAN.exe is not running. Start EPLAN normally, open the safe test project, and leave EPLAN running for the RemoteClient probe.'
}

$requiredRuntime = @(
    'Eplan.EplApi.RemoteClientu.dll',
    'Eplan.EplApi.Remotingu.dll'
)
foreach ($name in $requiredRuntime) {
    $source = Join-Path $EplanApi29Dir $name
    if (-not (Test-Path $source)) {
        throw "EPLAN RemoteClient assembly was not found: $source"
    }

    $deployed = Join-Path $outputDir $name
    if (-not (Test-Path $deployed)) {
        throw "LegacyHost runtime assembly is missing: $deployed. Pull the latest Task 6 branch and rerun scripts\verify-task6-build.ps1."
    }
}

# EPLAN 2.9 RemoteClient uses managed WCF assemblies deployed beside LegacyHost.
# Keep the EPLAN bin directory on PATH as an additional native/dependency resolution aid.
$oldPath = $env:Path
$env:Path = "$EplanApi29Dir;$env:Path"
$oldOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

try {
    Write-Host 'Running persistent Task 6 RemoteClient probe against the already-running EPLAN instance...'
    & $hostExe --task6-probe
    if ($LASTEXITCODE -ne 0) {
        throw "Task 6 RemoteClient probe failed with exit code $LASTEXITCODE."
    }

    Write-Host 'PASS: Task 6 RemoteClient probe completed successfully.'
}
finally {
    $env:Path = $oldPath
    [Console]::OutputEncoding = $oldOutputEncoding
}
