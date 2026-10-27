param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedProjectName,

    [Parameter(Mandatory = $true)]
    [string]$ConfirmedDisposableProjectName,

    [string]$TemporaryFunctionText = 'Audeu Gate C temporary Function Text'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$x64DotnetDir = 'C:\Program Files\dotnet'
$x64Dotnet = Join-Path $x64DotnetDir 'dotnet.exe'

if (Test-Path $x64Dotnet) {
    $env:Path = "$x64DotnetDir;$env:Path"
}

if ([string]::IsNullOrWhiteSpace($ExpectedProjectName)) {
    throw 'ExpectedProjectName must not be empty.'
}
if ([string]::IsNullOrWhiteSpace($ConfirmedDisposableProjectName)) {
    throw 'ConfirmedDisposableProjectName must not be empty.'
}
if ($ExpectedProjectName -cne $ConfirmedDisposableProjectName) {
    throw 'Gate C mutation refused: ConfirmedDisposableProjectName must exactly match ExpectedProjectName.'
}
if ([string]::IsNullOrEmpty($TemporaryFunctionText)) {
    throw 'TemporaryFunctionText must not be empty.'
}
if ($TemporaryFunctionText.Length -gt 1024) {
    throw 'TemporaryFunctionText must not exceed 1024 UTF-16 code units.'
}

if (-not (Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue)) {
    throw 'EPLAN.exe is not running. Start EPLAN normally, open ONLY the confirmed disposable Gate C project, select exactly one Function, and leave EPLAN running.'
}

$mcpExe = Join-Path $repoRoot 'src\Audeu.EplanAi.Mcp\bin\Release\net8.0\Audeu.EplanAi.Mcp.exe'
$probeExe = Join-Path $repoRoot 'test-support\Audeu.EplanAi.McpProbe\bin\Release\net8.0\Audeu.EplanAi.McpProbe.exe'

if (-not (Test-Path $mcpExe)) {
    throw "Task 12 MCP executable not found: $mcpExe. Close EPLAN and run scripts\verify-task9-build.ps1 first."
}
if (-not (Test-Path $probeExe)) {
    throw "Task 12 probe executable not found: $probeExe. Close EPLAN and run scripts\verify-task9-build.ps1 first."
}

$oldOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

try {
    Write-Host 'Running Task 12 Gate C through the official ModelContextProtocol stdio client.'
    Write-Host 'WARNING: this acceptance test performs real EPLAN writes, undo, and a controlled rollback test.'
    Write-Host 'The active project MUST be a disposable/safe copy.'
    Write-Host "Expected disposable project: $ExpectedProjectName"
    Write-Host "Explicit confirmation:       $ConfirmedDisposableProjectName"
    Write-Host "Temporary Function Text:     $TemporaryFunctionText"
    Write-Host 'Exactly one writable Function must be selected in EPLAN.'
    Write-Host ''

    $arguments = @(
        '--mode', 'gate-c',
        '--server', $mcpExe,
        '--expected-project-name', $ExpectedProjectName,
        '--confirmed-disposable-project-name', $ConfirmedDisposableProjectName,
        '--temporary-function-text', $TemporaryFunctionText
    )

    & $probeExe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Task 12 Gate C probe failed with exit code $LASTEXITCODE."
    }

    Write-Host 'PASS: Task 12 Gate C probe exited cleanly.'
    Write-Host 'Return to the LegacyHost PowerShell window and press Ctrl+C; it should report a clean stop.'
}
finally {
    $markerPath = Join-Path ([System.IO.Path]::GetTempPath()) 'Audeu.EplanAi.GateC.fail-next-write'
    if (Test-Path $markerPath) {
        Remove-Item -LiteralPath $markerPath -Force -ErrorAction SilentlyContinue
    }
    [Console]::OutputEncoding = $oldOutputEncoding
}
