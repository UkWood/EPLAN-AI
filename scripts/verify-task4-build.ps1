param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [switch]$Register,
    [int]$RegistrationTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj'

if (-not (Test-Path $EplanApi29Dir)) {
    throw "EPLAN API directory not found: $EplanApi29Dir"
}

Write-Host "Building Task 4 AddIn against: $EplanApi29Dir"
& dotnet build $project -c Release "-p:EplanApi29Dir=$EplanApi29Dir"
if ($LASTEXITCODE -ne 0) {
    throw "Task 4 AddIn build failed with exit code $LASTEXITCODE."
}

$addinDll = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\bin\Release\net472\Audeu.EplanAi.AddIn.dll'
if (-not (Test-Path $addinDll)) {
    throw "Build succeeded but AddIn DLL was not found: $addinDll"
}

Write-Host "PASS: real-API AddIn build produced $addinDll"

if (-not $Register) {
    Write-Host 'Registration was not requested. Re-run with -Register to call EplApiModuleAction.'
    exit 0
}

$runningEplan = Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue
if ($runningEplan) {
    throw 'EPLAN.exe is already running. Close EPLAN completely before -Register so registration can be validated in a clean process.'
}

$hostExe = Join-Path $EplanApi29Dir 'EPLAN.exe'
if (-not (Test-Path $hostExe)) {
    $hostExe = Join-Path $EplanApi29Dir 'W3u.exe'
}
if (-not (Test-Path $hostExe)) {
    throw "Neither EPLAN.exe nor W3u.exe was found in $EplanApi29Dir"
}

$diagnosticsDir = Join-Path $env:LOCALAPPDATA 'Audeu\EplanAiBridge\diagnostics'
$lifecycleLog = Join-Path $diagnosticsDir 'addin-lifecycle.log'
$actionLog = Join-Path $diagnosticsDir 'dispatch-action.log'
foreach ($logPath in @($lifecycleLog, $actionLog)) {
    if (Test-Path $logPath) {
        Remove-Item -Force $logPath
    }
}

$arguments = @(
    '/Variant:"Electric P8"',
    '/NoLoadWorkspace',
    '/NoSplash',
    '/Auto',
    'EplApiModuleAction',
    "/register:`"$addinDll`""
)

Write-Host 'Starting clean EPLAN registration via EplApiModuleAction...'
Write-Host "Waiting up to $RegistrationTimeoutSeconds seconds for EPLAN startup + registration..."
$process = Start-Process -FilePath $hostExe -ArgumentList $arguments -PassThru

if (-not $process.WaitForExit($RegistrationTimeoutSeconds * 1000)) {
    throw "EPLAN registration process did not exit within $RegistrationTimeoutSeconds seconds."
}

$deadline = (Get-Date).AddSeconds(5)
while ((Get-Date) -lt $deadline -and -not (Test-Path $lifecycleLog)) {
    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path $lifecycleLog)) {
    throw "EPLAN registration process exited, but no AddIn lifecycle log was produced at $lifecycleLog. The DLL was not loaded far enough to enter IEplAddIn lifecycle."
}

$lifecycle = Get-Content -Path $lifecycleLog
Write-Host 'AddIn lifecycle observed:'
$lifecycle | ForEach-Object { Write-Host "  $_" }

if ($lifecycle | Select-String -SimpleMatch '|OnRegister|') {
    Write-Host 'PASS: EPLAN performed fresh IEplAddIn.OnRegister registration.'
}
elseif ($lifecycle | Select-String -Pattern '\|(OnInit|OnInitGui|OnExit)\|') {
    Write-Host 'PASS: EPLAN loaded the AddIn lifecycle. OnRegister was not repeated, which is expected for an already registered AddIn.'
}
else {
    throw "AddIn lifecycle log exists but contains no expected registration/startup lifecycle entry: $lifecycleLog"
}

if (Test-Path $actionLog) {
    Write-Host 'Dispatch action diagnostics observed during registration/startup:'
    Get-Content -Path $actionLog | ForEach-Object { Write-Host "  $_" }
}
else {
    Write-Host 'INFO: no dispatch-action.log was produced during registration/startup. The action may be instantiated lazily; the round-trip probe will test that boundary.'
}

Write-Host 'Registration/load lifecycle verified. Next run scripts/test-task4-status-roundtrip.ps1.'
