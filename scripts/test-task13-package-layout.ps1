$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packager = Join-Path $PSScriptRoot 'New-Task13ReleaseBundle.ps1'

if (-not (Test-Path $packager)) {
    throw "Task 13 packager not found: $packager"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Audeu.EplanAi.Task13.PackageTest." + [Guid]::NewGuid().ToString('N'))

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

try {
    $addin = Join-Path $testRoot 'addin'
    $legacyHost = Join-Path $testRoot 'legacyhost'
    $mcp = Join-Path $testRoot 'mcp'
    $docs = Join-Path $testRoot 'docs'
    $output = Join-Path $testRoot 'output'

    foreach ($dir in @($addin, $legacyHost, $mcp, $docs, $output)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Set-Content -Path (Join-Path $addin 'Audeu.EplanAi.AddIn.dll') -Value 'addin-primary' -Encoding ASCII
    Set-Content -Path (Join-Path $addin 'Audeu.EplanAi.Contracts.dll') -Value 'contracts' -Encoding ASCII
    Set-Content -Path (Join-Path $legacyHost 'Audeu.EplanAi.LegacyHost.exe') -Value 'legacy-primary' -Encoding ASCII
    Set-Content -Path (Join-Path $legacyHost 'Audeu.EplanAi.Contracts.dll') -Value 'contracts' -Encoding ASCII
    Set-Content -Path (Join-Path $mcp 'Audeu.EplanAi.Mcp.exe') -Value 'mcp-primary' -Encoding ASCII
    Set-Content -Path (Join-Path $mcp 'Audeu.EplanAi.Mcp.dll') -Value 'mcp-dll' -Encoding ASCII

    $installationDoc = Join-Path $docs 'INSTALLATION.md'
    $troubleshootingDoc = Join-Path $docs 'TROUBLESHOOTING.md'
    $securityDoc = Join-Path $docs 'SECURITY.md'
    Set-Content -Path $installationDoc -Value '# Installation' -Encoding ASCII
    Set-Content -Path $troubleshootingDoc -Value '# Troubleshooting' -Encoding ASCII
    Set-Content -Path $securityDoc -Value '# Security' -Encoding ASCII

    & $packager `
        -AddInInputDir $addin `
        -LegacyHostInputDir $legacyHost `
        -McpInputDir $mcp `
        -OutputDir $output `
        -Version '0.1.0' `
        -InstallationDoc $installationDoc `
        -TroubleshootingDoc $troubleshootingDoc `
        -SecurityDoc $securityDoc

    $expectedArchives = @(
        'Audeu.EplanAi.AddIn-0.1.0.zip',
        'Audeu.EplanAi.LegacyHost-0.1.0.zip',
        'Audeu.EplanAi.Mcp-0.1.0-win-x64.zip',
        'EplanAiBridge-v0.1.0.zip'
    )

    foreach ($archive in $expectedArchives) {
        Assert-True (Test-Path (Join-Path $output $archive)) "Expected archive is missing: $archive"
    }

    $manifestPath = Join-Path $output 'SHA256SUMS.txt'
    Assert-True (Test-Path $manifestPath) 'SHA256SUMS.txt is missing.'
    $manifestLines = @(Get-Content -Path $manifestPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-True ($manifestLines.Count -eq 4) "Expected 4 checksum entries, got $($manifestLines.Count)."

    $manifestNames = @()
    foreach ($line in $manifestLines) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+\.zip)$') {
            throw "Invalid checksum manifest line: $line"
        }
        $manifestNames += $Matches[2]
    }

    $sortedNames = @($manifestNames | Sort-Object)
    Assert-True (($manifestNames -join "`n") -eq ($sortedNames -join "`n")) 'Checksum entries are not sorted by archive name.'
    Assert-True (($manifestNames -join "`n") -eq (($expectedArchives | Sort-Object) -join "`n")) 'Checksum manifest does not cover exactly the expected archives.'

    $expanded = Join-Path $testRoot 'expanded'
    Expand-Archive -Path (Join-Path $output 'EplanAiBridge-v0.1.0.zip') -DestinationPath $expanded -Force
    $bundleRoot = Join-Path $expanded 'EplanAiBridge-v0.1.0'

    $requiredBundlePaths = @(
        'addin\Audeu.EplanAi.AddIn.dll',
        'legacyhost\Audeu.EplanAi.LegacyHost.exe',
        'mcp\Audeu.EplanAi.Mcp.exe',
        'docs\INSTALLATION.md',
        'docs\TROUBLESHOOTING.md',
        'docs\SECURITY.md',
        'VERSION.txt'
    )

    foreach ($relativePath in $requiredBundlePaths) {
        Assert-True (Test-Path (Join-Path $bundleRoot $relativePath)) "Combined bundle path is missing: $relativePath"
    }

    Remove-Item -Force (Join-Path $addin 'Audeu.EplanAi.AddIn.dll')
    $missingOutput = Join-Path $testRoot 'missing-output'
    $missingRejected = $false
    try {
        & $packager `
            -AddInInputDir $addin `
            -LegacyHostInputDir $legacyHost `
            -McpInputDir $mcp `
            -OutputDir $missingOutput `
            -Version '0.1.0' `
            -InstallationDoc $installationDoc `
            -TroubleshootingDoc $troubleshootingDoc `
            -SecurityDoc $securityDoc
    }
    catch {
        if ($_.Exception.Message -like '*PACKAGE_INPUT_MISSING*') {
            $missingRejected = $true
        }
        else {
            throw
        }
    }

    Assert-True $missingRejected 'Missing AddIn primary binary was not rejected with PACKAGE_INPUT_MISSING.'
    Write-Host 'PASS: Task 13 package layout and checksum contract.'
}
finally {
    if (Test-Path $testRoot) {
        Remove-Item -Recurse -Force $testRoot
    }
}
