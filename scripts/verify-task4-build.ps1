param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [switch]$Register
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

$hostExe = Join-Path $EplanApi29Dir 'W3u.exe'
if (-not (Test-Path $hostExe)) {
    $hostExe = Join-Path $EplanApi29Dir 'EPLAN.exe'
}
if (-not (Test-Path $hostExe)) {
    throw "Neither W3u.exe nor EPLAN.exe was found in $EplanApi29Dir"
}

$arguments = @(
    '/Variant:"Electric P8"',
    'EplApiModuleAction',
    "/register:`"$addinDll`""
)

Write-Host "Starting EPLAN registration via EplApiModuleAction..."
Start-Process -FilePath $hostExe -ArgumentList $arguments
Write-Host 'Registration process started. Verify in EPLAN that Audeu.EplanAi.AddIn is loaded, then run scripts/test-task4-status-roundtrip.ps1.'
