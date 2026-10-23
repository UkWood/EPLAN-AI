param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$hostExe = Join-Path $EplanApi29Dir 'W3u.exe'
if (-not (Test-Path $hostExe)) {
    $hostExe = Join-Path $EplanApi29Dir 'EPLAN.exe'
}
if (-not (Test-Path $hostExe)) {
    throw "Neither W3u.exe nor EPLAN.exe was found in $EplanApi29Dir"
}

$requestId = [Guid]::NewGuid()
$root = Join-Path $env:LOCALAPPDATA 'Audeu\EplanAiBridge'
$requestDir = Join-Path $root 'requests'
$responseDir = Join-Path $root 'responses'
New-Item -ItemType Directory -Force -Path $requestDir | Out-Null
New-Item -ItemType Directory -Force -Path $responseDir | Out-Null

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
$request | ConvertTo-Json -Compress | Set-Content -Path $requestPath -Encoding UTF8

$arguments = @(
    '/Variant:"Electric P8"',
    'AudeuAiDispatch',
    "/REQUESTID:$($requestId.ToString('D'))"
)

Write-Host "Launching AudeuAiDispatch for request $requestId"
Start-Process -FilePath $hostExe -ArgumentList $arguments

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and -not (Test-Path $responsePath)) {
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path $responsePath)) {
    throw "No response was produced within $TimeoutSeconds seconds. Request: $requestPath"
}

$response = Get-Content -Raw -Path $responsePath | ConvertFrom-Json
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
