# Task 8 MCP Read-Only Tools Design

**Status:** approved for implementation  
**Date:** 2026-10-26  
**Parent spec:** `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`  
**Issue:** #20

## Goal

Expose the already-proven EPLAN read path as five local Model Context Protocol tools without allowing the MCP process to load or reference any EPLAN assembly.

## Architecture

`Audeu.EplanAi.Mcp` remains a .NET 8 process hosted over MCP stdio. It translates MCP tool calls into the existing versioned `BridgeRequest` contract, sends one framed request through the fixed current-user pipe `Audeu.EplanAi.LegacyHost.v1`, receives the matching `BridgeResponse`, and returns `DataJson` unchanged as compact JSON text.

The MCP layer is deliberately thin. EPLAN discovery, RemoteClient lifetime, request serialization, project-session validation, reference resolution, and DataModel access remain in LegacyHost/AddIn. No EPLAN API assembly is referenced by the MCP project.

## Components

### `Transport/IBridgeClient.cs`

Small testable boundary:

```text
Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
```

### `Transport/NamedPipeBridgeClient.cs`

Production implementation:
- fixed pipe name only: `Audeu.EplanAi.LegacyHost.v1`;
- server name `.` only;
- 10-second connect timeout;
- one connection per MCP tool invocation;
- canonical camelCase JSON compatible with Task 7;
- 4-byte little-endian frame length;
- request limit 1 MiB;
- response limit 16 MiB;
- verifies response `requestId` equals request `requestId`;
- verifies protocol version `1.0`.

Pipe names, hosts, filesystem paths, EPLAN action names, and EPLAN property IDs are never accepted from MCP arguments.

### `Tools/EplanReadTools.cs`

Expose exactly:
- `eplan_status` -> `system.status`;
- `eplan_get_active_project` -> `project.getActive`;
- `eplan_list_pages(projectSessionId)` -> `project.listPages`;
- `eplan_get_selection(projectSessionId)` -> `selection.get`;
- `eplan_get_object(projectSessionId, databaseIdentifier, typeIdentifier, objectIdentifier, stringIdentifier)` -> `object.get`.

Every tool is declared read-only and closed-world. Tool schemas use only strings/integers/Guids and contain no EPLAN types.

## Request construction

Every tool creates a new canonical `BridgeRequest`:
- `ProtocolVersion = "1.0"`;
- fresh `RequestId`;
- `Mode = "read"`;
- fixed operation name chosen by code;
- optional `ProjectSessionId` from the relevant tool argument;
- object reference values converted to invariant strings in the bridge `Arguments` dictionary.

## Result and error policy

Successful responses return `DataJson` exactly as compact JSON text. The MCP layer does not expand or reshape EPLAN payloads.

If `BridgeResponse.Success == false`, the MCP tool returns a controlled error whose message starts with the stable bridge code, e.g. `SESSION_STALE: ...`. Unknown/missing codes become `INTERNAL_ERROR`.

Local transport failures are also deterministic:
- connect timeout -> `OPERATION_TIMEOUT`;
- unavailable/broken pipe -> `EPLAN_NOT_CONNECTED`;
- malformed/oversized frame, request/response ID mismatch, or protocol mismatch -> `INTERNAL_ERROR`.

No stack trace, EPLAN assembly path, or raw exception details are exposed through the MCP tool result.

## MCP hosting

Use pinned `ModelContextProtocol` 2.3.0:
- `Host.CreateApplicationBuilder(args)`;
- logging, if any, goes to stderr;
- `AddMcpServer().WithStdioServerTransport().WithTools<EplanReadTools>()`;
- register `IBridgeClient` -> `NamedPipeBridgeClient` in DI.

Nothing except valid MCP protocol traffic may be written to stdout.

## Tests

1. Stream-level pipe codec produces/consumes Task 7-compatible camelCase framed JSON and enforces size limits.
2. Named-pipe client preserves `requestId`, rejects mismatched IDs/protocol, and maps timeout/unavailable transport deterministically.
3. Tool mapping creates exactly the five whitelisted bridge operations with `Mode = read`.
4. Session-aware tools require/forward `projectSessionId`.
5. `eplan_get_object` forwards only the compact reference fields expected by Task 5.
6. Bridge failure codes are preserved in controlled MCP errors.
7. Reflection/SDK metadata proves exactly five tools are exposed and all are `ReadOnly=true`.
8. CI runs MCP tests, not only the MCP build.

## Explicit non-goals

- No write tools.
- No Change Plan support.
- No HTTP/TCP listener.
- No caller-selected pipe name.
- No arbitrary bridge operation forwarding.
- No arbitrary EPLAN action/property forwarding.
- No raw EPLAN DataModel types.

Task 9 remains the real workstation MCP-to-EPLAN M0 acceptance gate before write work begins.