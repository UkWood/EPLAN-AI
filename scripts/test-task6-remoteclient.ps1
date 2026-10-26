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

Write-Host 'EPLAN process diagnostics:'
try {
    $processes = Get-CimInstance Win32_Process -Filter "Name='EPLAN.exe'" -ErrorAction Stop
    if ($processes) {
        foreach ($process in $processes) {
            Write-Host ("  PID {0}: {1}" -f $process.ProcessId, $process.CommandLine)
        }
    }
    else {
        Write-Host '  <no Win32_Process entry found>'
    }
}
catch {
    Write-Host ("  INFO: unable to read EPLAN command line: {0}" -f $_.Exception.Message)
}

$remoteServerRoot = 'HKCU:\Software\EPLAN\RemoteServer'
Write-Host "EPLAN RemoteServer registry diagnostics: $remoteServerRoot"
if (Test-Path $remoteServerRoot) {
    $serverKeys = Get-ChildItem $remoteServerRoot -ErrorAction SilentlyContinue
    if ($serverKeys) {
        foreach ($key in $serverKeys) {
            Write-Host ("  key: {0}" -f $key.PSChildName)
            try {
                $properties = Get-ItemProperty $key.PSPath -ErrorAction Stop
                foreach ($property in $properties.PSObject.Properties) {
                    if ($property.Name -notlike 'PS*') {
                        Write-Host ("    {0} = {1}" -f $property.Name, $property.Value)
                    }
                }
            }
            catch {
                Write-Host ("    INFO: unable to read key values: {0}" -f $_.Exception.Message)
            }
        }
    }
    else {
        Write-Host '  <root exists but contains no active server keys>'
    }
}
else {
    Write-Host '  <RemoteServer registry root is missing>'
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
