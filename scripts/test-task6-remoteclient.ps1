param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$hostExe = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472\Audeu.EplanAi.LegacyHost.exe'

if (-not (Test-Path $hostExe)) {
    throw "Task 6 LegacyHost executable not found: $hostExe. Run scripts\verify-task6-build.ps1 first."
}

$runningEplan = Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue
if (-not $runningEplan) {
    throw 'EPLAN.exe is not running. Start EPLAN normally, open the safe test project, and leave EPLAN running for the RemoteClient probe.'
}

$remoteClientDll = Join-Path $EplanApi29Dir 'Eplan.EplApi.RemoteClientu.dll'
$remotingDll = Join-Path $EplanApi29Dir 'Eplan.EplApi.Remotingu.dll'
if (-not (Test-Path $remoteClientDll) -or -not (Test-Path $remotingDll)) {
    throw "EPLAN RemoteClient assemblies were not found in $EplanApi29Dir"
}

# Keep the probe environment explicit. The production host deployment is finalized in Task 7;
# for Task 6 we prepend the verified EPLAN API directory for native/dependency resolution.
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
