# Task 6 EPLAN RemoteClient Gateway Design

**Date:** 2026-10-26  
**Status:** approved by existing EPLAN AI Bridge 0.1 design + user instruction to continue  
**Tracks:** Issue #16

## Intent

Implement the LegacyHost -> EPLAN Remoting boundary while preserving the existing secure spool and fixed `AudeuAiDispatch` action. Task 6 must keep one EPLAN runtime connection alive long enough to validate Task 5 read operations in one project session.

## Goals

- Discover active EPLAN remoting servers on the local machine.
- Select a deterministic EPLAN 2.9 server candidate.
- Connect and disconnect cleanly.
- Set `SynchronousMode = true` before action execution.
- Ping the connected server.
- Execute exactly `AudeuAiDispatch /REQUESTID:<canonical-guid>`.
- Use the existing `SpoolStore` for request/response payloads.
- Map no server, connection, action and timeout failures to stable bridge errors.
- Keep the connection alive across multiple bridge requests.

## Non-goals

- No named pipe yet (Task 7).
- No MCP exposure yet (Task 8).
- No starting/stopping EPLAN automatically in production gateway v0.1.
- No arbitrary EPLAN action input.
- No engineering/DataModel logic in LegacyHost.
- No writes.

## Architecture

### `IRemoteClientAdapter`
Testable boundary around the EPLAN 2.9 API. It exposes only:
- discovery of local servers;
- connect/disconnect;
- synchronous-mode setting;
- ping;
- fixed action execution.

### `EplanRemoteClientAdapter`
The only class that references `Eplan.EplApi.RemoteClient`. It projects `EplanServerData` to internal `RemoteServerInfo` and calls the documented EPLAN 2.9 API.

### `RemoteGateway`
Owns connection state and bridge policy. It chooses a local EPLAN 2.9 server deterministically (eligible candidates ordered by server port, then process ID), connects with a bounded timeout, requires ping success, writes a request to the spool, invokes the fixed action, waits for the response, and returns a `BridgeResponse`.

## Fixed action rule

The only outgoing action string is constructed internally:

```text
AudeuAiDispatch /REQUESTID:<guid-D>
```

No caller can supply an action name, command fragment, port, host, or filesystem path to `ExecuteBridgeRequest`.

## Error mapping

- no eligible active local EPLAN 2.9 server -> `EPLAN_NOT_FOUND`;
- connect/ping failure or lost connection -> `EPLAN_NOT_CONNECTED`;
- `ExecuteAction` false/exception -> `EPLAN_ACTION_FAILED`;
- operation exceeds 60 seconds -> `OPERATION_TIMEOUT`;
- unexpected host/spool failures -> `INTERNAL_ERROR`.

Errors are returned as `BridgeResponse`; stack traces are not exposed.

## Timeout policy

EPLAN 2.9 exposes a timeout on `Connect`, but not a documented per-action timeout. The gateway therefore runs synchronous `ExecuteAction` behind a bounded host-side wait. On timeout the current client is treated as poisoned: the gateway disconnects/disposes it and requires reconnection before any later request. Request IDs keep any late spool response isolated from subsequent requests.

## Test strategy

CI uses a test-only fake adapter; production builds still require real `RemoteClientu.dll` and `Remotingu.dll`. Tests prove:
- deterministic discovery/selection;
- `SynchronousMode=true` before action execution;
- exact fixed action string;
- ping/connect/disconnect behavior;
- stable error mapping;
- timeout invalidates the connection;
- response request-ID pairing remains enforced by `SpoolStore`.

Real workstation validation compiles against the installed EPLAN 2.9.4 DLLs, then connects to an already-running EPLAN instance and performs `system.status` plus the Task 5 read sequence in the same runtime.