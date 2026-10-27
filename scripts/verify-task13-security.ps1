param(
    [string]$SourceRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src')
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceRoot) -or -not (Test-Path $SourceRoot)) {
    throw "SECURITY_SOURCE_MISSING: Source root was not found: $SourceRoot"
}

$sourceFiles = @(
    Get-ChildItem -Path $SourceRoot -Recurse -File | Where-Object {
        $_.Extension -eq '.cs' -or $_.Extension -eq '.csproj'
    }
)

if ($sourceFiles.Count -eq 0) {
    throw "SECURITY_SOURCE_EMPTY: No C# source files were found under $SourceRoot"
}

function Assert-NoPattern {
    param(
        [System.IO.FileInfo[]]$Files,
        [string[]]$Patterns,
        [string]$ErrorCode,
        [string]$Description
    )

    foreach ($file in $Files) {
        $content = Get-Content -Path $file.FullName -Raw
        foreach ($pattern in $Patterns) {
            if ($content -match $pattern) {
                throw "$ErrorCode: $Description detected in $($file.FullName). Matched pattern: $pattern"
            }
        }
    }
}

$listenerPatterns = @(
    '\bTcpListener\b',
    '\bTcpClient\b',
    '\bHttpListener\b',
    '\bWebApplication\b',
    '\bSocket\s*\('
)
Assert-NoPattern -Files $sourceFiles -Patterns $listenerPatterns -ErrorCode 'SECURITY_TCP_LISTENER' -Description 'Network listener/client primitive'
Write-Host 'PASS: Gate D security - no TCP/web listener primitives in bridge source.'

$dynamicCodePatterns = @(
    '\bAssembly\s*\.\s*Load(?:From|File)?\s*\(',
    '\bProcess\s*\.\s*Start\s*\(',
    '\bPowerShell\s*\.\s*Create\s*\(',
    '\bCSharpCodeProvider\b',
    '\bInvoke-Expression\b',
    '\bcmd\.exe\b',
    '\bpowershell\.exe\b'
)
Assert-NoPattern -Files $sourceFiles -Patterns $dynamicCodePatterns -ErrorCode 'SECURITY_DYNAMIC_CODE' -Description 'Dynamic code or shell/process execution primitive'
Write-Host 'PASS: Gate D security - no dynamic code loading or shell/process execution primitives in bridge source.'

$mcpToolFiles = @()
foreach ($file in $sourceFiles | Where-Object { $_.Extension -eq '.cs' }) {
    $content = Get-Content -Path $file.FullName -Raw
    if ($content -match '\[McpServerTool\b') {
        $mcpToolFiles += $file
    }
}

if ($mcpToolFiles.Count -eq 0) {
    throw 'SECURITY_MCP_TOOLS_MISSING: No McpServerTool definitions were found.'
}

$genericInputPatterns = @(
    '\b(?:string|int|uint|long|object)\s+(?:propertyId|actionName|operationName|path|filePath|filesystemPath|command|code|script)\b',
    '\bOpenWorld\s*=\s*true\b'
)
Assert-NoPattern -Files $mcpToolFiles -Patterns $genericInputPatterns -ErrorCode 'SECURITY_GENERIC_INPUT' -Description 'Generic model-controlled MCP input or open-world tool'
Write-Host 'PASS: Gate D security - MCP tools expose no generic property/action/operation/path/code input.'

Write-Host "PASS: Gate D security review ($($sourceFiles.Count) source files, $($mcpToolFiles.Count) MCP tool files)."
