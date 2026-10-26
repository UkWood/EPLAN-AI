param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$x64DotnetDir = 'C:\Program Files\dotnet'
$x64Dotnet = Join-Path $x64DotnetDir 'dotnet.exe'

if (Test-Path $x64Dotnet) {
    $env:Path = "$x64DotnetDir;$env:Path"
}

$dotnet = Get-Command dotnet -ErrorAction Stop
Write-Host "Using dotnet: $($dotnet.Source)"
$sdkList = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0) {
    throw "dotnet --list-sdks failed with exit code $LASTEXITCODE."
}
if (-not ($sdkList | Where-Object { $_ -match '^8\.0\.' })) {
    Write-Host ($sdkList -join [Environment]::NewLine)
    throw 'Task 9 requires an x64 .NET 8 SDK. No 8.0 SDK was visible to the selected dotnet host.'
}
Write-Host 'PASS: .NET 8 SDK is visible.'

& (Join-Path $PSScriptRoot 'verify-task7-build.ps1') -EplanApi29Dir $EplanApi29Dir
if ($LASTEXITCODE -ne 0) {
    throw "Task 7 LegacyHost prerequisite build failed with exit code $LASTEXITCODE."
}

$mcpProject = Join-Path $repoRoot 'src\Audeu.EplanAi.Mcp\Audeu.EplanAi.Mcp.csproj'
$probeProject = Join-Path $repoRoot 'test-support\Audeu.EplanAi.McpProbe\Audeu.EplanAi.McpProbe.csproj'

Write-Host 'Building Task 8 MCP server for Gate B...'
& dotnet build $mcpProject -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Task 8 MCP server build failed with exit code $LASTEXITCODE."
}

Write-Host 'Building Task 9 official MCP workstation probe...'
& dotnet build $probeProject -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Task 9 MCP probe build failed with exit code $LASTEXITCODE."
}

$legacyHostExe = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472\Audeu.EplanAi.LegacyHost.exe'
$mcpExe = Join-Path $repoRoot 'src\Audeu.EplanAi.Mcp\bin\Release\net8.0\Audeu.EplanAi.Mcp.exe'
$probeExe = Join-Path $repoRoot 'test-support\Audeu.EplanAi.McpProbe\bin\Release\net8.0\Audeu.EplanAi.McpProbe.exe'

foreach ($path in @($legacyHostExe, $mcpExe, $probeExe)) {
    if (-not (Test-Path $path)) {
        throw "Expected Task 9 runtime executable was not produced: $path"
    }
}

Write-Host "PASS: LegacyHost ready: $legacyHostExe"
Write-Host "PASS: MCP server ready: $mcpExe"
Write-Host "PASS: Task 9 official MCP probe ready: $probeExe"
Write-Host 'Next: start EPLAN/open the safe project/select exactly one Function. In window 1 run scripts\run-task7-legacyhost.ps1. In window 2 run scripts\test-task9-mcp-readonly.ps1.'
