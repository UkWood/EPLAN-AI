$ErrorActionPreference = 'Stop'

$PipeName = 'Audeu.EplanAi.LegacyHost.v1'
$MaxRequestBytes = 1024 * 1024
$MaxResponseBytes = 16 * 1024 * 1024
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$oldOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = $Utf8

function Read-Exact {
    param(
        [System.IO.Stream]$Stream,
        [int]$Length
    )

    [byte[]]$buffer = New-Object byte[] $Length
    $offset = 0
    while ($offset -lt $Length) {
        $read = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($read -le 0) {
            throw "Named pipe closed before $Length bytes were read."
        }
        $offset += $read
    }
    return $buffer
}

function Send-BridgeRequest {
    param(
        [string]$Operation,
        [string]$ProjectSessionId = $null,
        [hashtable]$Arguments = @{}
    )

    $requestId = [Guid]::NewGuid().ToString('D')
    $request = [ordered]@{
        protocolVersion = '1.0'
        requestId       = $requestId
        operation       = $Operation
        mode            = 'read'
        arguments       = $Arguments
    }
    if (-not [string]::IsNullOrWhiteSpace($ProjectSessionId)) {
        $request.projectSessionId = $ProjectSessionId
    }

    $json = $request | ConvertTo-Json -Depth 10 -Compress
    [byte[]]$payload = $Utf8.GetBytes($json)
    if ($payload.Length -le 0 -or $payload.Length -gt $MaxRequestBytes) {
        throw "Task 7 request payload length $($payload.Length) is outside the allowed range."
    }

    $client = New-Object System.IO.Pipes.NamedPipeClientStream(
        '.',
        $PipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::Asynchronous)

    try {
        try {
            $client.Connect(5000)
        }
        catch {
            throw "LegacyHost named pipe '$PipeName' is unavailable. Start scripts\run-task7-legacyhost.ps1 in another PowerShell window. $($_.Exception.Message)"
        }

        [byte[]]$prefix = New-Object byte[] 4
        $length = $payload.Length
        $prefix[0] = [byte]($length -band 0xff)
        $prefix[1] = [byte](($length -shr 8) -band 0xff)
        $prefix[2] = [byte](($length -shr 16) -band 0xff)
        $prefix[3] = [byte](($length -shr 24) -band 0xff)

        $client.Write($prefix, 0, $prefix.Length)
        $client.Write($payload, 0, $payload.Length)
        $client.Flush()

        [byte[]]$responsePrefix = Read-Exact -Stream $client -Length 4
        $responseLength = [int]$responsePrefix[0] `
            -bor ([int]$responsePrefix[1] -shl 8) `
            -bor ([int]$responsePrefix[2] -shl 16) `
            -bor ([int]$responsePrefix[3] -shl 24)

        if ($responseLength -le 0 -or $responseLength -gt $MaxResponseBytes) {
            throw "LegacyHost returned invalid response frame length $responseLength."
        }

        [byte[]]$responsePayload = Read-Exact -Stream $client -Length $responseLength
        $responseJson = $Utf8.GetString($responsePayload)
        $response = $responseJson | ConvertFrom-Json

        if ([string]$response.requestId -ne $requestId) {
            throw "LegacyHost response requestId mismatch. Expected $requestId, got $($response.requestId)."
        }
        if (-not $response.success) {
            throw "Bridge returned failure for $Operation`: $($response.errorCode) $($response.errorMessage)"
        }

        return $response
    }
    finally {
        $client.Dispose()
    }
}

try {
    Write-Host "Running Task 7 client through fixed current-user named pipe: $PipeName"
    Write-Host 'NOTE: this PowerShell client does not load or reference any EPLAN API assembly.'

    $statusResponse = Send-BridgeRequest -Operation 'system.status'
    $status = $statusResponse.dataJson | ConvertFrom-Json
    Write-Host "PASS: system.status through named pipe (status=$($status.status), protocol=$($status.protocolVersion))"

    $activeResponse = Send-BridgeRequest -Operation 'project.getActive'
    $project = $activeResponse.dataJson | ConvertFrom-Json
    $sessionId = [string]$project.projectSessionId
    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        throw 'project.getActive returned an empty projectSessionId.'
    }

    Write-Host 'PASS: project.getActive through named pipe'
    Write-Host "  projectSessionId : $sessionId"
    Write-Host "  databaseId       : $($project.databaseIdentifier)"
    Write-Host "  name             : $($project.name)"
    Write-Host "  pageCount        : $($project.pageCount)"

    $pagesResponse = Send-BridgeRequest -Operation 'project.listPages' -ProjectSessionId $sessionId
    $pages = $pagesResponse.dataJson | ConvertFrom-Json
    if ([string]$pages.projectSessionId -ne $sessionId) {
        throw 'project.listPages did not preserve the active projectSessionId.'
    }
    Write-Host "PASS: project.listPages through named pipe with same projectSessionId (items=$(@($pages.items).Count))"

    $selectionResponse = Send-BridgeRequest -Operation 'selection.get' -ProjectSessionId $sessionId
    $selection = $selectionResponse.dataJson | ConvertFrom-Json
    if ([string]$selection.projectSessionId -ne $sessionId) {
        throw 'selection.get did not preserve the active projectSessionId.'
    }
    Write-Host "PASS: selection.get through named pipe with same projectSessionId (items=$(@($selection.items).Count))"

    $function = @($selection.items) | Where-Object { $_.kind -eq 'Function' -and $null -ne $_.reference } | Select-Object -First 1
    if ($null -ne $function) {
        $reference = $function.reference
        $objectArguments = @{
            databaseIdentifier = [string]$reference.databaseIdentifier
            typeIdentifier     = [string]$reference.typeIdentifier
            objectIdentifier   = [string]$reference.objectIdentifier
            stringIdentifier   = [string]$reference.stringIdentifier
        }
        $objectResponse = Send-BridgeRequest -Operation 'object.get' -ProjectSessionId $sessionId -Arguments $objectArguments
        $object = $objectResponse.dataJson | ConvertFrom-Json
        if ([string]$object.reference.projectSessionId -ne $sessionId) {
            throw 'object.get returned a reference from a different projectSessionId.'
        }
        Write-Host "PASS: object.get through named pipe for selected Function: $($object.name)"
    }
    else {
        Write-Host 'INFO: no Function selected; object.get named-pipe probe skipped.'
    }

    Write-Host 'PASS: Task 7 named-pipe persistent read sequence completed successfully.'
    Write-Host 'Return to the LegacyHost PowerShell window and press Ctrl+C; it should report a clean stop.'
}
finally {
    [Console]::OutputEncoding = $oldOutputEncoding
}
