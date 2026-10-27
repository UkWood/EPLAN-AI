param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [string]$Version = '0.1.0',
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\release')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$x64DotnetDir = 'C:\Program Files\dotnet'
$x64Dotnet = Join-Path $x64DotnetDir 'dotnet.exe'

if (Test-Path $x64Dotnet -PathType Leaf) {
    $env:Path = "$x64DotnetDir;$env:Path"
    $dotnet = $x64Dotnet
}
else {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

function Invoke-TestProject {
    param(
        [string]$Description,
        [string]$Project,
        [string[]]$ExtraArguments = @()
    )

    Write-Host $Description
    $arguments = @('test', $Project, '-c', 'Release') + $ExtraArguments
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GATE_D_TEST_FAILED: $Description failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'Running Gate D non-EPLAN automated verification...'
Invoke-TestProject `
    -Description 'Contracts tests' `
    -Project (Join-Path $repoRoot 'tests\Audeu.EplanAi.Contracts.Tests\Audeu.EplanAi.Contracts.Tests.csproj')
Invoke-TestProject `
    -Description 'LegacyHost tests' `
    -Project (Join-Path $repoRoot 'tests\Audeu.EplanAi.LegacyHost.Tests\Audeu.EplanAi.LegacyHost.Tests.csproj') `
    -ExtraArguments @('-p:SkipEplanApiValidation=true')
Invoke-TestProject `
    -Description 'AddIn integration tests with API stubs' `
    -Project (Join-Path $repoRoot 'tests\Audeu.EplanAi.AddIn.IntegrationTests\Audeu.EplanAi.AddIn.IntegrationTests.csproj') `
    -ExtraArguments @('-p:UseEplanApiStubs=true')
Invoke-TestProject `
    -Description 'MCP tests' `
    -Project (Join-Path $repoRoot 'tests\Audeu.EplanAi.Mcp.Tests\Audeu.EplanAi.Mcp.Tests.csproj')
Invoke-TestProject `
    -Description 'MCP workstation probe tests' `
    -Project (Join-Path $repoRoot 'tests\Audeu.EplanAi.McpProbe.Tests\Audeu.EplanAi.McpProbe.Tests.csproj')
Write-Host 'PASS: Gate D non-EPLAN automated test suites.'

& (Join-Path $PSScriptRoot 'verify-task13-security.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "GATE_D_SECURITY_FAILED: Security verifier exited with code $LASTEXITCODE."
}

$projectState = Join-Path $repoRoot 'docs\PROJECT_STATE.md'
if (-not (Test-Path $projectState -PathType Leaf)) {
    throw "GATE_D_EVIDENCE_MISSING: Project state file was not found: $projectState"
}
$stateText = Get-Content -Path $projectState -Raw
if ($stateText -notmatch '(?im)Gate B[^\r\n]*PASS' -or $stateText -notmatch '(?im)Gate C[^\r\n]*PASS') {
    throw 'GATE_D_EVIDENCE_MISSING: Recorded Gate B and Gate C PASS evidence is required before Gate D.'
}
Write-Host 'PASS: recorded real-workstation Gate B and Gate C acceptance evidence is present.'

$git = Get-Command git -ErrorAction Stop
$insideWorkTree = (& $git.Source -C $repoRoot rev-parse --is-inside-work-tree 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $insideWorkTree -ne 'true') {
    throw 'GATE_D_GIT_REQUIRED: Gate D must run from the EPLAN-AI Git working tree.'
}

$baseRef = $null
& $git.Source -C $repoRoot rev-parse --verify origin/main 1>$null 2>$null
if ($LASTEXITCODE -eq 0) {
    $baseRef = 'origin/main'
}
else {
    & $git.Source -C $repoRoot rev-parse --verify main 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) {
        $baseRef = 'main'
    }
}
if ([string]::IsNullOrWhiteSpace($baseRef)) {
    throw 'GATE_D_BASE_MISSING: Could not resolve origin/main or main for the Task 13 runtime-source diff guard.'
}

$mergeBase = (& $git.Source -C $repoRoot merge-base HEAD $baseRef | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mergeBase)) {
    throw "GATE_D_BASE_MISSING: Could not compute merge base against $baseRef."
}
$range = $mergeBase + '..HEAD'
$runtimeChanges = @(& $git.Source -C $repoRoot diff --name-only $range -- src)
if ($LASTEXITCODE -ne 0) {
    throw 'GATE_D_GIT_FAILED: Could not inspect Task 13 runtime source changes.'
}
$runtimeChanges = @($runtimeChanges | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($runtimeChanges.Count -gt 0) {
    Write-Host ($runtimeChanges -join [Environment]::NewLine)
    throw 'GATE_D_RUNTIME_REACCEPTANCE_REQUIRED: Task 13 changes runtime source under src/. Repeat the applicable real Gate B/Gate C acceptance before release.'
}
Write-Host 'PASS: Task 13 does not modify runtime source under src/; accepted Gate B/Gate C runtime evidence remains applicable.'

& (Join-Path $PSScriptRoot 'build-task13-release.ps1') `
    -EplanApi29Dir $EplanApi29Dir `
    -Version $Version `
    -OutputRoot $OutputRoot
if ($LASTEXITCODE -ne 0) {
    throw "GATE_D_RELEASE_BUILD_FAILED: Release builder exited with code $LASTEXITCODE."
}

$releaseDir = Join-Path $OutputRoot "eplan-ai-bridge-v$Version"
$expectedFiles = @(
    "Audeu.EplanAi.AddIn-$Version.zip",
    "Audeu.EplanAi.LegacyHost-$Version.zip",
    "Audeu.EplanAi.Mcp-$Version-win-x64.zip",
    "EplanAiBridge-v$Version.zip",
    'SHA256SUMS.txt'
)
foreach ($name in $expectedFiles) {
    $path = Join-Path $releaseDir $name
    if (-not (Test-Path $path -PathType Leaf)) {
        throw "GATE_D_RELEASE_ARTIFACT_MISSING: Expected release file was not found: $path"
    }
}

Write-Host "PASS: Gate D release artifact set is complete at $releaseDir"
Write-Host 'PASS: Gate D repository and real-EPLAN release verification completed successfully.'
Write-Host 'NOTE: Gate B/Gate C runtime acceptance was not re-simulated; it remains the previously accepted real-workstation evidence because Task 13 did not modify src/.'
