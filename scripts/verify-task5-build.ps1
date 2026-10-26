param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj'

if (-not (Test-Path $EplanApi29Dir)) {
    throw "EPLAN API directory not found: $EplanApi29Dir"
}

Write-Host "Building Task 5 AddIn against real EPLAN API: $EplanApi29Dir"
& dotnet build $project -c Release "-p:EplanApi29Dir=$EplanApi29Dir"
if ($LASTEXITCODE -ne 0) {
    throw "Task 5 AddIn build failed with exit code $LASTEXITCODE."
}

$addinDll = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\bin\Release\net472\Audeu.EplanAi.AddIn.dll'
if (-not (Test-Path $addinDll)) {
    throw "Build succeeded but AddIn DLL was not found: $addinDll"
}

Write-Host "PASS: Task 5 AddIn compiled against real EPLAN 2.9.4 assemblies: $addinDll"
Write-Host 'Next: run scripts\test-task5-active-project.ps1 with EPLAN closed after a project has been opened at least once.'
