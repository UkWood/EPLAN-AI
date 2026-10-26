param(
    [string]$ExpectedProjectName = 'УСПД Жуковский ПНС',
    [int]$ExpectedPageCount = 7,
    [string]$ExpectedFunctionText = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$x64DotnetDir = 'C:\Program Files\dotnet'
$x64Dotnet = Join-Path $x64DotnetDir 'dotnet.exe'

if (Test-Path $x64Dotnet) {
    $env:Path = "$x64DotnetDir;$env:Path"
}

if (-not (Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue)) {
    throw 'EPLAN.exe is not running. Start EPLAN normally, open the safe test project, select exactly one Function, and leave EPLAN running.'
}

if ([string]::IsNullOrEmpty($ExpectedFunctionText)) {
    $ExpectedFunctionText = Read-Host 'Enter the exact Function Text of the single Function currently selected in EPLAN'
}
if ([string]::IsNullOrEmpty($ExpectedFunctionText)) {
    throw 'Expected Function Text must not be empty. Select a Function with known non-empty Function Text for Gate B.'
}
if ($ExpectedPageCount -le 0) {
    throw 'ExpectedPageCount must be a positive integer.'
}

$mcpExe = Join-Path $repoRoot 'src\Audeu.EplanAi.Mcp\bin\Release\net8.0\Audeu.EplanAi.Mcp.exe'
$probeExe = Join-Path $repoRoot 'test-support\Audeu.EplanAi.McpProbe\bin\Release\net8.0\Audeu.EplanAi.McpProbe.exe'

if (-not (Test-Path $mcpExe)) {
    throw "Task 8 MCP executable not found: $mcpExe. Run scripts\verify-task9-build.ps1 first."
}
if (-not (Test-Path $probeExe)) {
    throw "Task 9 probe executable not found: $probeExe. Run scripts\verify-task9-build.ps1 first."
}

$oldOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

try {
    Write-Host 'Running Task 9 Gate B through the official ModelContextProtocol stdio client.'
    Write-Host 'NOTE: the probe does not load or reference any EPLAN API assembly.'
    Write-Host "Expected project: $ExpectedProjectName"
    Write-Host "Expected page count: $ExpectedPageCount"
    Write-Host 'The probe will pause twice for project close/reopen actions in EPLAN.'
    Write-Host ''

    $arguments = @(
        '--server', $mcpExe,
        '--expected-project-name', $ExpectedProjectName,
        '--expected-page-count', $ExpectedPageCount.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        '--expected-function-text', $ExpectedFunctionText
    )

    & $probeExe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Task 9 Gate B probe failed with exit code $LASTEXITCODE."
    }

    Write-Host 'PASS: Task 9 Gate B probe exited cleanly.'
    Write-Host 'Return to the LegacyHost PowerShell window and press Ctrl+C; it should report a clean stop.'
}
finally {
    [Console]::OutputEncoding = $oldOutputEncoding
}
