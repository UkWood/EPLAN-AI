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
if (Test-Path $lifecycleLog) {
    Remove-Item -Force $lifecycleLog
}

$arguments = @(
    '/Variant:"Electric P8"',
    '/NoLoadWorkspace',
    '/NoSplash',
    '/Auto',
    'EplApiModuleAction',
    "/register:`"$addinDll`""
)

Write-Host "Starting clean EPLAN registration via EplApiModuleAction..."
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
    throw "EPLAN registration process exited, but IEplAddIn.OnRegister did not produce $lifecycleLog. The add-in DLL was not loaded far enough to enter its lifecycle. Check EPLAN system messages and module loading prerequisites."
}

$lifecycle = Get-Content -Path $lifecycleLog
if (-not ($lifecycle | Select-String -SimpleMatch '|OnRegister|')) {
    throw "Lifecycle log exists but contains no OnRegister entry: $lifecycleLog"
}

Write-Host 'PASS: EPLAN called IEplAddIn.OnRegister.'
$lifecycle | ForEach-Object { Write-Host "  $_" }
Write-Host 'Registration lifecycle verified. Next run scripts/test-task4-status-roundtrip.ps1.'
