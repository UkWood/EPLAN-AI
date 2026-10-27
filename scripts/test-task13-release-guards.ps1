$ErrorActionPreference = 'Stop'

$builder = Join-Path $PSScriptRoot 'build-task13-release.ps1'
if (-not (Test-Path $builder)) {
    throw "Task 13 release builder not found: $builder"
}

$missingApi = Join-Path ([System.IO.Path]::GetTempPath()) ('Audeu.EplanAi.NoSuchApi.' + [Guid]::NewGuid().ToString('N'))
$rejected = $false
try {
    & $builder -EplanApi29Dir $missingApi -Version '0.1.0'
}
catch {
    if ($_.Exception.Message -like '*RELEASE_EPLAN_API_MISSING*') {
        $rejected = $true
    }
    else {
        throw
    }
}

if (-not $rejected) {
    throw 'Missing EPLAN API directory was not rejected with RELEASE_EPLAN_API_MISSING.'
}

$builderText = Get-Content -Path $builder -Raw
foreach ($requiredCode in @('RELEASE_EPLAN_RUNNING', 'RELEASE_LEGACYHOST_RUNNING')) {
    if (-not $builderText.Contains($requiredCode)) {
        throw "Release builder does not expose required process guard code: $requiredCode"
    }
}

Write-Host 'PASS: Task 13 release builder fails closed on missing EPLAN API and defines EPLAN/LegacyHost process guards.'
