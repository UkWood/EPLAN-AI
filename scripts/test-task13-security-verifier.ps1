$ErrorActionPreference = 'Stop'

$verifier = Join-Path $PSScriptRoot 'verify-task13-security.ps1'
if (-not (Test-Path $verifier)) {
    throw "Task 13 security verifier not found: $verifier"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Audeu.EplanAi.Task13.SecurityTest." + [Guid]::NewGuid().ToString('N'))

function New-Fixture {
    param(
        [string]$Name,
        [string]$Content,
        [string]$RelativePath = 'Audeu.EplanAi.Mcp\Tools\FixtureTools.cs'
    )

    $root = Join-Path $testRoot $Name
    $file = Join-Path $root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
    Set-Content -Path $file -Value $Content -Encoding ASCII
    return $root
}

function Assert-Rejected {
    param(
        [string]$SourceRoot,
        [string]$ExpectedCode
    )

    $rejected = $false
    try {
        & $verifier -SourceRoot $SourceRoot
    }
    catch {
        if ($_.Exception.Message -like "*$ExpectedCode*") {
            $rejected = $true
        }
        else {
            throw
        }
    }

    if (-not $rejected) {
        throw "Expected security rejection $ExpectedCode was not observed."
    }
}

try {
    $safe = New-Fixture -Name 'safe' -Content @'
using ModelContextProtocol.Server;
public static class FixtureTools
{
    [McpServerTool(Name = "eplan_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public static string Status() => "ready";
}
'@
    & $verifier -SourceRoot $safe

    $tcp = New-Fixture -Name 'tcp' -RelativePath 'Bridge\Listener.cs' -Content @'
using System.Net.Sockets;
public sealed class Listener { private readonly TcpListener _listener = null; }
'@
    Assert-Rejected -SourceRoot $tcp -ExpectedCode 'SECURITY_TCP_LISTENER'

    $dynamic = New-Fixture -Name 'dynamic' -RelativePath 'Bridge\Loader.cs' -Content @'
using System.Reflection;
public static class Loader { public static void Run(byte[] bytes) => Assembly.Load(bytes); }
'@
    Assert-Rejected -SourceRoot $dynamic -ExpectedCode 'SECURITY_DYNAMIC_CODE'

    $generic = New-Fixture -Name 'generic' -Content @'
using ModelContextProtocol.Server;
public static class FixtureTools
{
    [McpServerTool(Name = "eplan_generic", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    public static string Run(string propertyId) => propertyId;
}
'@
    Assert-Rejected -SourceRoot $generic -ExpectedCode 'SECURITY_GENERIC_INPUT'

    Write-Host 'PASS: Task 13 security verifier accepts the closed-world fixture and rejects forbidden classes.'
}
finally {
    if (Test-Path $testRoot) {
        Remove-Item -Recurse -Force $testRoot
    }
}
