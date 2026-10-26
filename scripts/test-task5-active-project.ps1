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
    throw 'EPLAN.exe is already running. Close EPLAN completely before the Task 5 active-project smoke probe.'
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
    Operation = 'project.getActive'
    Mode = 'read'
    ProjectSessionId = $null
    Arguments = @{}
}
$requestJson = $request | ConvertTo-Json -Compress
[IO.File]::WriteAllText($requestPath, $requestJson, (New-Object Text.UTF8Encoding($false)))

$arguments = @(
    '/Variant:"Electric P8"',
    '/NoSplash',
    '/UseLastOpenedProjects:1',
    '/Auto',
    'AudeuAiDispatch',
    "/REQUESTID:$($requestId.ToString('D'))"
)

Write-Host "Launching Task 5 project.getActive for request $requestId"
Write-Host 'EPLAN will reopen the last-used project(s) for this command-line run.'
Write-Host "Waiting up to $TimeoutSeconds seconds for EPLAN startup + read operation..."
$process = Start-Process -FilePath $hostExe -ArgumentList $arguments -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and -not (Test-Path $responsePath)) {
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path $responsePath)) {
    $processState = if ($process.HasExited) { "EPLAN exited with code $($process.ExitCode)" } else { 'EPLAN process is still running' }
    throw "No Task 5 response was produced within $TimeoutSeconds seconds. $processState. Request: $requestPath"
}

$responseJson = [IO.File]::ReadAllText($responsePath, [Text.Encoding]::UTF8)
$response = $responseJson | ConvertFrom-Json
if ([string]$response.RequestId -ne $requestId.ToString()) {
    throw "Response request ID mismatch. Expected $requestId, got $($response.RequestId)."
}
if (-not $response.Success) {
    if ($response.ErrorCode -eq 'NO_ACTIVE_PROJECT') {
        throw 'Task 5 reached EPLAN but no active project was available. Open a test project once, close EPLAN normally, and rerun this probe; /UseLastOpenedProjects:1 is already enabled.'
    }
    throw "Task 5 bridge returned failure: $($response.ErrorCode) $($response.ErrorMessage)"
}

$data = $response.DataJson | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$data.projectSessionId)) {
    throw "project.getActive returned no projectSessionId: $($response.DataJson)"
}
if ([uint32]$data.databaseIdentifier -eq 0) {
    throw "project.getActive returned an invalid databaseIdentifier: $($response.DataJson)"
}

Write-Host "PASS: Task 5 project.getActive succeeded for request $requestId"
Write-Host "  projectSessionId : $($data.projectSessionId)"
Write-Host "  databaseId      : $($data.databaseIdentifier)"
Write-Host "  name            : $($data.name)"
Write-Host "  fullName        : $($data.fullName)"
Write-Host "  pageCount       : $($data.pageCount)"
Write-Host "  isReadOnly      : $($data.isReadOnly)"
Write-Host 'NOTE: listPages/selection/object persistent-session runtime validation is deferred to Task 6 RemoteClient; CI already covers their session/error semantics.'
