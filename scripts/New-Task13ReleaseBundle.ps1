param(
    [Parameter(Mandatory = $true)]
    [string]$AddInInputDir,

    [Parameter(Mandatory = $true)]
    [string]$LegacyHostInputDir,

    [Parameter(Mandatory = $true)]
    [string]$McpInputDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$Version = '0.1.0',

    [string]$InstallationDoc,

    [string]$TroubleshootingDoc,

    [string]$SecurityDoc
)

$ErrorActionPreference = 'Stop'

function Require-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path $Path)) {
        throw "PACKAGE_INPUT_MISSING: $Description was not found: $Path"
    }
}

function Remove-IfExists {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Recurse -Force $Path
    }
}

Require-Path $AddInInputDir 'AddIn input directory'
Require-Path $LegacyHostInputDir 'LegacyHost input directory'
Require-Path $McpInputDir 'MCP input directory'
Require-Path (Join-Path $AddInInputDir 'Audeu.EplanAi.AddIn.dll') 'AddIn primary binary'
Require-Path (Join-Path $LegacyHostInputDir 'Audeu.EplanAi.LegacyHost.exe') 'LegacyHost primary binary'
Require-Path (Join-Path $McpInputDir 'Audeu.EplanAi.Mcp.exe') 'MCP primary binary'
Require-Path $InstallationDoc 'installation document'
Require-Path $TroubleshootingDoc 'troubleshooting document'
Require-Path $SecurityDoc 'security document'

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$addinArchiveName = "Audeu.EplanAi.AddIn-$Version.zip"
$legacyHostArchiveName = "Audeu.EplanAi.LegacyHost-$Version.zip"
$mcpArchiveName = "Audeu.EplanAi.Mcp-$Version-win-x64.zip"
$bundleArchiveName = "EplanAiBridge-v$Version.zip"
$archiveNames = @(
    $addinArchiveName,
    $legacyHostArchiveName,
    $mcpArchiveName,
    $bundleArchiveName
)

foreach ($name in $archiveNames) {
    Remove-IfExists (Join-Path $OutputDir $name)
}

$manifestPath = Join-Path $OutputDir 'SHA256SUMS.txt'
Remove-IfExists $manifestPath

$stagingRoot = Join-Path $OutputDir '.task13-staging'
Remove-IfExists $stagingRoot

try {
    $bundleName = "EplanAiBridge-v$Version"
    $bundleRoot = Join-Path $stagingRoot $bundleName
    $bundleAddIn = Join-Path $bundleRoot 'addin'
    $bundleLegacyHost = Join-Path $bundleRoot 'legacyhost'
    $bundleMcp = Join-Path $bundleRoot 'mcp'
    $bundleDocs = Join-Path $bundleRoot 'docs'

    foreach ($dir in @($bundleAddIn, $bundleLegacyHost, $bundleMcp, $bundleDocs)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Copy-Item -Path (Join-Path $AddInInputDir '*') -Destination $bundleAddIn -Recurse -Force
    Copy-Item -Path (Join-Path $LegacyHostInputDir '*') -Destination $bundleLegacyHost -Recurse -Force
    Copy-Item -Path (Join-Path $McpInputDir '*') -Destination $bundleMcp -Recurse -Force

    Copy-Item -Path $InstallationDoc -Destination (Join-Path $bundleDocs 'INSTALLATION.md') -Force
    Copy-Item -Path $TroubleshootingDoc -Destination (Join-Path $bundleDocs 'TROUBLESHOOTING.md') -Force
    Copy-Item -Path $SecurityDoc -Destination (Join-Path $bundleDocs 'SECURITY.md') -Force
    Set-Content -Path (Join-Path $bundleRoot 'VERSION.txt') -Value $Version -Encoding ASCII

    $addinArchive = Join-Path $OutputDir $addinArchiveName
    $legacyHostArchive = Join-Path $OutputDir $legacyHostArchiveName
    $mcpArchive = Join-Path $OutputDir $mcpArchiveName
    $bundleArchive = Join-Path $OutputDir $bundleArchiveName

    Compress-Archive -Path (Join-Path $AddInInputDir '*') -DestinationPath $addinArchive -CompressionLevel Optimal -Force
    Compress-Archive -Path (Join-Path $LegacyHostInputDir '*') -DestinationPath $legacyHostArchive -CompressionLevel Optimal -Force
    Compress-Archive -Path (Join-Path $McpInputDir '*') -DestinationPath $mcpArchive -CompressionLevel Optimal -Force
    Compress-Archive -Path $bundleRoot -DestinationPath $bundleArchive -CompressionLevel Optimal -Force

    $manifestLines = @()
    foreach ($name in ($archiveNames | Sort-Object)) {
        $archivePath = Join-Path $OutputDir $name
        Require-Path $archivePath "release archive $name"
        $hash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
        $manifestLines += "$hash  $name"
    }

    Set-Content -Path $manifestPath -Value $manifestLines -Encoding ASCII

    Write-Host "PASS: Task 13 release bundle packaged at $OutputDir"
    Write-Host "PASS: SHA-256 manifest contains $($manifestLines.Count) sorted archive entries"
}
finally {
    Remove-IfExists $stagingRoot
}
