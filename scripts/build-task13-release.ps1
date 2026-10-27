param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin',
    [string]$Version = '0.1.0',
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\release')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "RELEASE_VERSION_INVALID: Expected semantic version X.Y.Z, got '$Version'."
}

if ([string]::IsNullOrWhiteSpace($EplanApi29Dir) -or -not (Test-Path $EplanApi29Dir -PathType Container)) {
    throw "RELEASE_EPLAN_API_MISSING: EPLAN 2.9 API directory was not found: $EplanApi29Dir"
}

$requiredEplanAssemblies = @(
    'Eplan.EplApi.AFu.dll',
    'Eplan.EplApi.Baseu.dll',
    'Eplan.EplApi.DataModelu.dll',
    'Eplan.EplApi.HEServicesu.dll',
    'Eplan.EplApi.RemoteClientu.dll',
    'Eplan.EplApi.Remotingu.dll'
)
foreach ($assembly in $requiredEplanAssemblies) {
    $assemblyPath = Join-Path $EplanApi29Dir $assembly
    if (-not (Test-Path $assemblyPath -PathType Leaf)) {
        throw "RELEASE_EPLAN_API_MISSING: Required EPLAN 2.9 assembly was not found: $assemblyPath"
    }
}
Write-Host 'PASS: verified EPLAN 2.9.4 API assembly set.'

if (Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue) {
    throw 'RELEASE_EPLAN_RUNNING: Close EPLAN completely before building the v0.1 release so AddIn binaries cannot be stale or locked.'
}
if (Get-Process -Name 'Audeu.EplanAi.LegacyHost' -ErrorAction SilentlyContinue) {
    throw 'RELEASE_LEGACYHOST_RUNNING: Stop the persistent LegacyHost before building the v0.1 release.'
}
Write-Host 'PASS: EPLAN and LegacyHost are stopped for a clean release build.'

$x64DotnetDir = 'C:\Program Files\dotnet'
$x64Dotnet = Join-Path $x64DotnetDir 'dotnet.exe'
if (Test-Path $x64Dotnet -PathType Leaf) {
    $env:Path = "$x64DotnetDir;$env:Path"
    $dotnet = $x64Dotnet
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

$sdkList = @(& $dotnet --list-sdks)
if ($LASTEXITCODE -ne 0) {
    throw "RELEASE_DOTNET_FAILED: dotnet --list-sdks failed with exit code $LASTEXITCODE."
}
if (-not ($sdkList | Where-Object { $_ -match '^8\.0\.' })) {
    throw 'RELEASE_DOTNET8_MISSING: An x64 .NET 8 SDK is required to publish the MCP server.'
}
Write-Host "PASS: using .NET host $dotnet with an 8.0 SDK."

$installationDoc = Join-Path $repoRoot 'docs\release\v0.1\INSTALLATION.md'
$troubleshootingDoc = Join-Path $repoRoot 'docs\release\v0.1\TROUBLESHOOTING.md'
$securityDoc = Join-Path $repoRoot 'docs\release\v0.1\SECURITY.md'
foreach ($doc in @($installationDoc, $troubleshootingDoc, $securityDoc)) {
    if (-not (Test-Path $doc -PathType Leaf)) {
        throw "RELEASE_DOC_MISSING: Required release document was not found: $doc"
    }
}

$packager = Join-Path $PSScriptRoot 'New-Task13ReleaseBundle.ps1'
if (-not (Test-Path $packager -PathType Leaf)) {
    throw "RELEASE_PACKAGER_MISSING: Release packager was not found: $packager"
}

$addinProject = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj'
$legacyHostProject = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\Audeu.EplanAi.LegacyHost.csproj'
$mcpProject = Join-Path $repoRoot 'src\Audeu.EplanAi.Mcp\Audeu.EplanAi.Mcp.csproj'

$buildRoot = Join-Path $OutputRoot ('.task13-build-' + [Guid]::NewGuid().ToString('N'))
$addinStage = Join-Path $buildRoot 'addin'
$legacyHostStage = Join-Path $buildRoot 'legacyhost'
$mcpStage = Join-Path $buildRoot 'mcp'
$releaseDir = Join-Path $OutputRoot "eplan-ai-bridge-v$Version"

if (Test-Path $releaseDir) {
    Remove-Item -Recurse -Force $releaseDir
}
New-Item -ItemType Directory -Path $addinStage -Force | Out-Null
New-Item -ItemType Directory -Path $legacyHostStage -Force | Out-Null
New-Item -ItemType Directory -Path $mcpStage -Force | Out-Null

function Invoke-DotnetChecked {
    param(
        [string]$Description,
        [string[]]$Arguments
    )

    Write-Host $Description
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "RELEASE_BUILD_FAILED: $Description failed with exit code $LASTEXITCODE."
    }
}

try {
    Invoke-DotnetChecked -Description 'Building AddIn against the real EPLAN 2.9.4 API...' -Arguments @(
        'build', $addinProject, '-c', 'Release', '--no-incremental',
        "-p:EplanApi29Dir=$EplanApi29Dir", "-p:Version=$Version"
    )

    Invoke-DotnetChecked -Description 'Building LegacyHost against the real EPLAN 2.9.4 API...' -Arguments @(
        'build', $legacyHostProject, '-c', 'Release', '--no-incremental',
        "-p:EplanApi29Dir=$EplanApi29Dir", "-p:Version=$Version"
    )

    Invoke-DotnetChecked -Description 'Publishing MCP server for win-x64 (.NET 8 framework-dependent)...' -Arguments @(
        'publish', $mcpProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false',
        '--no-restore', "-p:Version=$Version", '-o', $mcpStage
    )

    $addinOutput = Join-Path $repoRoot 'src\Audeu.EplanAi.AddIn\bin\Release\net472'
    $legacyHostOutput = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472'

    foreach ($required in @(
        (Join-Path $addinOutput 'Audeu.EplanAi.AddIn.dll'),
        (Join-Path $addinOutput 'Audeu.EplanAi.Contracts.dll'),
        (Join-Path $legacyHostOutput 'Audeu.EplanAi.LegacyHost.exe'),
        (Join-Path $mcpStage 'Audeu.EplanAi.Mcp.exe')
    )) {
        if (-not (Test-Path $required -PathType Leaf)) {
            throw "RELEASE_ARTIFACT_MISSING: Expected fresh release artifact was not produced: $required"
        }
    }

    foreach ($pattern in @('Audeu.EplanAi.AddIn.*', 'Audeu.EplanAi.Contracts.*')) {
        Get-ChildItem -Path $addinOutput -Filter $pattern -File | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $addinStage -Force
        }
    }
    Copy-Item -Path (Join-Path $legacyHostOutput '*') -Destination $legacyHostStage -Recurse -Force

    $forbiddenAddInAssemblies = @(
        'Eplan.EplApi.AFu.dll',
        'Eplan.EplApi.Baseu.dll',
        'Eplan.EplApi.DataModelu.dll',
        'Eplan.EplApi.HEServicesu.dll'
    )
    foreach ($assembly in $forbiddenAddInAssemblies) {
        if (Test-Path (Join-Path $addinStage $assembly)) {
            throw "RELEASE_ADDIN_EPLAN_DLL_FORBIDDEN: AddIn package must not redistribute $assembly"
        }
    }
    Write-Host 'PASS: AddIn staging contains no redistributed EPLAN DataModel/HEServices platform assemblies.'

    & $packager `
        -AddInInputDir $addinStage `
        -LegacyHostInputDir $legacyHostStage `
        -McpInputDir $mcpStage `
        -OutputDir $releaseDir `
        -Version $Version `
        -InstallationDoc $installationDoc `
        -TroubleshootingDoc $troubleshootingDoc `
        -SecurityDoc $securityDoc

    $manifest = Join-Path $releaseDir 'SHA256SUMS.txt'
    if (-not (Test-Path $manifest -PathType Leaf)) {
        throw "RELEASE_CHECKSUM_MISSING: Checksum manifest was not produced: $manifest"
    }

    foreach ($line in Get-Content -Path $manifest) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') {
            throw "RELEASE_CHECKSUM_INVALID: Invalid manifest line: $line"
        }
        $expectedHash = $Matches[1].ToUpperInvariant()
        $archiveName = $Matches[2]
        $archivePath = Join-Path $releaseDir $archiveName
        if (-not (Test-Path $archivePath -PathType Leaf)) {
            throw "RELEASE_CHECKSUM_MISSING: Manifest archive was not found: $archivePath"
        }
        $actualHash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "RELEASE_CHECKSUM_MISMATCH: SHA-256 mismatch for $archiveName"
        }
    }

    Write-Host 'PASS: release SHA-256 manifest independently verified.'
    Write-Host "PASS: Task 13 v0.1 release bundle built and verified: $releaseDir"
}
finally {
    if (Test-Path $buildRoot) {
        Remove-Item -Recurse -Force $buildRoot
    }
}
