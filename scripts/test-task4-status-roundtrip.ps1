param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$hostExe = Join-Path $EplanApi29Dir 'EPLAN.exe'
if (-not (Test-Path $hostExe)) {
    $hostExe = Join-Path $EplanApi29Dir 'W3u.exe'
}
if (-not (Test-Path $hostExe)) {
    throw "Neither EPLAN.exe nor W3u.exe was found in $EplanApi29Dir"
}

$runningEplan = Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue
if ($runningEplan) {
    throw 'EPLAN.exe is already running. Close EPLAN completely before the Task 4 round-trip so the command-line action runs in a clean process.'
}

$requestId = [Guid]::NewGuid()
$root = Join-Path $env:LOCALAPPDATA 'Audeu\EplanAiBridge'
$requestDir = Join-Path $root 'requests'
$responseDir = Join-Path $root 'responses'
$diagnosticsDir = Join-Path $root 'diagnostics'
$lifecycleLog = Join-Path $diagnosticsDir 'addin-lifecycle.log'
$actionLog = Join-Path $diagnosticsDir 'dispatch-action.log'
New-Item -ItemType Directory -Force -Path $requestDir | Out-Null
New-Item -ItemType Directory -Force -Path $responseDir | Out-Null
New-Item -ItemType Directory -Force -Path $diagnosticsDir | Out-Null
foreach ($logPath in @($lifecycleLog, $actionLog)) {
    if (Test-Path $logPath) {
        Remove-Item -Force $logPath
    }
}

$requestPath = Join-Path $requestDir ($requestId.ToString('D') + '.request.json')
$responsePath = Join-Path $responseDir ($requestId.ToString('D') + '.response.json')

$request = [ordered]@{
    ProtocolVersion = '1.0'
    RequestId = $requestId
    Operation = 'system.status'
    Mode = 'read'
    ProjectSessionId = $null
    Arguments = @{}
}
$requestJson = $request | ConvertTo-Json -Compress
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($requestPath, $requestJson, $utf8NoBom)

$arguments = @(
    '/Variant:"Electric P8"',
    '/NoLoadWorkspace',
    '/NoSplash',
    '/Auto',
    'AudeuAiDispatch',
    "/REQUESTID:$($requestId.ToString('D'))"
)

Write-Host "Launching AudeuAiDispatch for request $requestId"
Write-Host "Waiting up to $TimeoutSeconds seconds for EPLAN startup + action execution..."
$process = Start-Process -FilePath $hostExe -ArgumentList $arguments -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and -not (Test-Path $responsePath)) {
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path $responsePath)) {
    $processState = if ($process.HasExited) { "EPLAN exited with code $($process.ExitCode)" } else { 'EPLAN process is still running' }
    $lifecycleText = if (Test-Path $lifecycleLog) {
        (Get-Content -Path $lifecycleLog | Select-Object -Last 20) -join [Environment]::NewLine
    } else {
        '<no add-in lifecycle log>'
    }
    $actionText = if (Test-Path $actionLog) {
        (Get-Content -Path $actionLog | Select-Object -Last 30) -join [Environment]::NewLine
    } else {
        '<no dispatch action log>'
    }

    throw "No response was produced within $TimeoutSeconds seconds. $processState. Request: $requestPath`nAddIn lifecycle:`n$lifecycleText`nDispatch action:`n$actionText"
}

$responseJson = [System.IO.File]::ReadAllText($responsePath, [System.Text.Encoding]::UTF8)
$response = $responseJson | ConvertFrom-Json
if ([string]$response.RequestId -ne $requestId.ToString()) {
    throw "Response request ID mismatch. Expected $requestId, got $($response.RequestId)."
}
if (-not $response.Success) {
    throw "Bridge returned failure: $($response.ErrorCode) $($response.ErrorMessage)"
}
if ($response.DataJson -notmatch '"status":"ready"') {
    throw "Unexpected system.status payload: $($response.DataJson)"
}

Write-Host "PASS: AudeuAiDispatch system.status round-trip succeeded for $requestId"
if (Test-Path $actionLog) {
    Write-Host 'Dispatch action diagnostics:'
    Get-Content -Path $actionLog | ForEach-Object { Write-Host "  $_" }
}
