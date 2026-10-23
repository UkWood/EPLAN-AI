# EPLAN AI Bridge 0.1 — Technical Design

**Date:** 2026-10-23  
**Target:** EPLAN Platform 2.9  
**Status:** approved for implementation

## 1. Purpose

Build a local, controlled bridge that lets an AI assistant inspect an open EPLAN 2.9 project and perform one deliberately narrow write operation through Preview → Apply → verification → rollback/undo.

Version 0.1 is a vertical prototype. It proves the complete path from an MCP client to EPLAN DataModel and back without attempting automatic schematic generation yet.

## 2. Scope

### Read capabilities
- report EPLAN connection/status and versions;
- get the active project;
- list project pages;
- inspect the current EPLAN selection;
- inspect a selected object;
- identify Function objects and return a controlled set of useful properties.

### Write capability
- prepare a change plan that changes Function Text for one selected Function;
- preview old/new values without modifying EPLAN;
- apply only an already-created change plan;
- verify the post-condition by reading the object again;
- rollback if execution fails;
- expose a user undo operation for the last AI change where feasible.

### Explicit non-goals for 0.1
- arbitrary property editing;
- arbitrary EPLAN Action execution from the LLM;
- creation/deletion of pages;
- device creation;
- macro insertion;
- connection creation;
- article selection;
- report generation;
- automatic engineering decisions;
- remote network access;
- HTTP server inside the EPLAN process.

## 3. Architecture

```text
ChatGPT / MCP client
        |
        | stdio MCP
        v
Audeu.EplanAi.Mcp             .NET 8
        |
        | local named pipe, current-user only
        v
Audeu.EplanAi.LegacyHost      .NET Framework 4.7.2
        |
        | EPLAN Remoting / WCF
        v
EPLAN 2.9
  Audeu.EplanAi.AddIn         .NET Framework 4.7.2
        |
        | DataModel / HEServices
        v
Open EPLAN project
```

The LegacyHost writes request/response payloads to a local spool directory and invokes a fixed EPLAN Action containing only a request GUID.

```text
%LOCALAPPDATA%\Audeu\EplanAiBridge\
  requests\<guid>.request.json
  responses\<guid>.response.json
```

The EPLAN add-in never accepts arbitrary source code, arbitrary method names, arbitrary EPLAN Action names, or arbitrary property IDs.

## 4. Technology choices

- EPLAN Add-in: C#, .NET Framework 4.7.2.
- LegacyHost: C#, .NET Framework 4.7.2.
- Shared contracts: C#, .NET Standard 2.0.
- MCP server: C#, .NET 8.
- MCP SDK: `ModelContextProtocol` 2.3.0, pinned.
- Tests: xUnit.
- JSON in the EPLAN process: BCL-compatible serialization; no mandatory third-party serializer in the initial add-in.
- Transport between MCP and LegacyHost: Windows Named Pipes.
- Transport between LegacyHost and EPLAN: EPLAN RemoteClient.
- EPLAN dispatch entry point: one fixed action named `AudeuAiDispatch`.

## 5. EPLAN prerequisites

Development and runtime installation must have the EPLAN 2.9 API assemblies required by the add-in and Remoting client. API-dependent components are built against the EPLAN 2.9 environment and .NET Framework 4.7.2.

Before implementation, verify on the target workstation:
1. exact installed EPLAN 2.9 build;
2. EPLAN API / API Extension licensing available for the intended add-in;
3. paths to the required API assemblies;
4. ability to load a minimal signed/unsigned test add-in in that installation;
5. ability to connect with `EplanRemoteClient` to a running instance.

If API Extension licensing is unavailable, stop the add-in path and re-scope to a reduced script/action prototype; do not silently substitute GUI automation.

## 6. Process boundaries

### `Audeu.EplanAi.Mcp`
Owns:
- MCP tools;
- user-facing schemas;
- change-plan lifecycle;
- policy and confirmation state;
- conversion between typed MCP results and bridge requests.

Does not reference EPLAN assemblies.

### `Audeu.EplanAi.LegacyHost`
Owns:
- named-pipe server;
- EPLAN discovery and connection;
- RemoteClient lifecycle;
- request spool writing;
- execution of the fixed `AudeuAiDispatch` action;
- response spool reading.

Contains no engineering logic.

### `Audeu.EplanAi.AddIn`
Owns:
- `IEplAddIn`;
- `AudeuAiDispatch : IEplAction`;
- request validation;
- DataModel access;
- object serialization;
- controlled write execution;
- transaction / SafetyPoint / Undo integration.

Does not host HTTP, MCP, LLM, or internet access.

## 7. Wire protocol

Protocol version: `1.0`.

### Request envelope

```json
{
  "protocolVersion": "1.0",
  "requestId": "7d64550e-9154-4d78-beb0-a622905a1372",
  "operation": "project.listPages",
  "mode": "read",
  "projectSessionId": "a4d841ab-2291-4e0d-b102-83fe6e52d897",
  "arguments": {}
}
```

For 0.1, `arguments` is restricted to string key/value pairs. This keeps the .NET Framework-side contract deliberately simple.

### Response envelope

```json
{
  "protocolVersion": "1.0",
  "requestId": "7d64550e-9154-4d78-beb0-a622905a1372",
  "success": true,
  "errorCode": null,
  "errorMessage": null,
  "dataJson": "{\"pages\":[]}",
  "warnings": []
}
```

### Fixed operation names

Read:
- `system.status`
- `project.getActive`
- `project.listPages`
- `selection.get`
- `object.get`

Change planning:
- `change.previewFunctionText`

Apply:
- `change.apply`
- `change.undoLast`

Unknown operations must return `BRIDGE_UNKNOWN_OPERATION`. They must never be forwarded as EPLAN Action names.

## 8. Request spool rules

- request ID must be a canonical GUID;
- filenames are generated by the bridge, never accepted as user paths;
- writes use `*.tmp` + atomic rename;
- maximum request size for 0.1: 1 MiB;
- maximum response size for 0.1: 16 MiB;
- spool root is fixed under `%LOCALAPPDATA%`;
- stale files older than 24 hours are cleaned on LegacyHost startup;
- request files are deleted after a successful round-trip;
- failed responses are retained for diagnostics up to the cleanup horizon.

## 9. Named-pipe rules

Pipe name: `Audeu.EplanAi.LegacyHost.v1`.

Requirements:
- local machine only;
- ACL restricted to the current Windows user;
- one request per message;
- maximum message size 1 MiB;
- 10 s connect timeout;
- 60 s operation timeout for 0.1;
- one in-flight EPLAN request at a time.

Parallel MCP calls are serialized by LegacyHost. This avoids ambiguous interactions with a single interactive EPLAN process.

## 10. Project session model

A project session ID is ephemeral.

Inside the add-in, maintain a map:

```text
EPLAN DatabaseIdentifier -> generated Guid
```

When the active project's database identifier changes, create a new session GUID.

Returned references are valid only while their `projectSessionId` matches the active project session.

Never persist EPLAN object references as durable AI memory.

## 11. Object references

```json
{
  "projectSessionId": "a4d841ab-2291-4e0d-b102-83fe6e52d897",
  "databaseIdentifier": 7,
  "typeIdentifier": 27,
  "objectIdentifier": 1482,
  "stringIdentifier": "..."
}
```

Validation before use:
1. session matches;
2. database identifier matches the active project;
3. object can be reconstructed/resolved;
4. `IsValid == true`;
5. object type is allowed for the requested operation.

For write operations in 0.1, the target must be a `Function`.

## 12. MCP tools in 0.1

### `eplan_status`
Returns:
- bridge connected;
- EPLAN connected;
- EPLAN version;
- add-in version;
- protocol version;
- active project present;
- write mode availability.

### `eplan_get_active_project`
Returns:
- session ID;
- project name/path;
- page count;
- read-only state when available.

### `eplan_list_pages`
Returns a compact page list:
- ordinal;
- full page name;
- description/type when available.

### `eplan_get_selection`
Returns selected objects with:
- kind;
- object reference;
- device/function name when applicable;
- page;
- compact description.

### `eplan_get_object`
Input: current-session object reference.  
Returns a whitelist of object properties. It must not dump every EPLAN property.

### `eplan_preview_set_function_text`
Input:
- target object reference;
- new text.

Returns:
- change plan ID;
- target summary;
- old value;
- new value;
- risk = `low`;
- no EPLAN modification.

### `eplan_apply_change_plan`
Input: change plan ID.

The MCP server accepts only a plan it created in the current MCP process and whose target project session still matches.

### `eplan_undo_last_ai_change`
Available only when the add-in reports a valid last undo step for the current session.

## 13. Change plan

```json
{
  "changePlanId": "0ef50e3f-f736-4195-b1f2-797ed9cd9851",
  "createdUtc": "2026-10-23T12:00:00Z",
  "projectSessionId": "a4d841ab-2291-4e0d-b102-83fe6e52d897",
  "status": "previewed",
  "operations": [
    {
      "kind": "setFunctionText",
      "target": { "...": "..." },
      "oldValue": "Pump contactor",
      "newValue": "Контактор насоса №1"
    }
  ]
}
```

Change plans are immutable. Applying a plan changes its state from `previewed` to `applied` or `failed`. A plan cannot be applied twice.

## 14. Write execution sequence

For `change.apply`:

```text
validate plan/session
    |
resolve Function
    |
capture old value
    |
create UndoStep
    |
create SafetyPoint
    |
create API Transaction
    |
set Function Text
    |
read value back
    |
if post-condition fails -> no Commit
    |
Transaction.Commit
    |
SafetyPoint.Commit
    |
return applied result
```

Implementation must account for EPLAN internal transactions. If an API call introduces an internal transaction, detect and surface that condition rather than assuming atomicity.

## 15. Error model

Stable error codes:
- `BRIDGE_NOT_CONNECTED`
- `EPLAN_NOT_FOUND`
- `EPLAN_NOT_CONNECTED`
- `EPLAN_ACTION_FAILED`
- `NO_ACTIVE_PROJECT`
- `SESSION_STALE`
- `OBJECT_NOT_FOUND`
- `OBJECT_INVALID`
- `OBJECT_TYPE_NOT_ALLOWED`
- `READ_ONLY_PROJECT`
- `PLAN_NOT_FOUND`
- `PLAN_ALREADY_APPLIED`
- `PLAN_STALE`
- `VALIDATION_FAILED`
- `OPERATION_TIMEOUT`
- `PROTOCOL_MISMATCH`
- `BRIDGE_UNKNOWN_OPERATION`
- `INTERNAL_ERROR`

User-facing messages must not expose stack traces. Diagnostic logs may contain stack traces locally.

## 16. Logging

Structured local logs:
- timestamp;
- request ID;
- operation;
- duration;
- project session ID;
- success/error code.

Do not log:
- prompts;
- arbitrary project property dumps;
- credentials;
- entire project exports.

Default log retention: 14 days.

## 17. Security constraints

- no listener on TCP;
- no remote network mode in 0.1;
- pipe ACL current user only;
- no arbitrary EPLAN action execution;
- no arbitrary DLL loading;
- no arbitrary filesystem path passed through MCP;
- no arbitrary property ID exposed to MCP;
- no arbitrary C# execution;
- writes require a change plan;
- a change plan is bound to a project session;
- stale object references fail closed.

## 18. Tests

### Unit/contract tests
Run without EPLAN:
- request/response serialization;
- protocol version mismatch;
- GUID/path validation;
- size limits;
- change-plan state machine;
- MCP tool mapping;
- stale session rejection;
- unknown operation rejection.

### Local integration tests
Require EPLAN 2.9:
- add-in load;
- action registration;
- RemoteClient discovery/connect/ping;
- `system.status`;
- active project;
- page enumeration;
- selection inspection;
- selected Function inspection;
- preview Function Text;
- apply Function Text;
- post-read verification;
- rollback on injected failure;
- undo last AI change.

Integration tests run against a disposable copy of an EPLAN test project, never a production project.

## 19. Definition of Done for 0.1

0.1 is complete when, on a real EPLAN 2.9 workstation:

1. MCP server starts without EPLAN assemblies in its process.
2. LegacyHost detects and connects to EPLAN 2.9.
3. Add-in registers `AudeuAiDispatch`.
4. AI can report active project and list pages.
5. AI can report the user's current selection.
6. A selected `Function` can be inspected.
7. AI can create a preview changing Function Text without modifying the project.
8. After explicit confirmation, the exact previewed change is applied.
9. The new value is read back and matches.
10. A deliberately injected failure leaves the original value intact.
11. Stale object/session references are rejected.
12. The last successful AI change can be undone or the product clearly reports why EPLAN no longer exposes that undo step.
13. No arbitrary Action/property/code execution path exists.

## 20. Next release boundary

Only after 0.1 is proven should 0.2 add:
- object search;
- device-level semantic model;
- page creation/copy;
- macro catalog inspection/insertion;
- deterministic engineering commands.

Automatic schematic generation is explicitly deferred until the macro/domain layer exists.

## 21. Verified EPLAN API assumptions

The design relies on documented EPLAN 2.9 behavior:
- add-ins implement `IEplAddIn`;
- custom actions implement `IEplAction`;
- Action parameters use `ActionCallingContext`;
- RemoteClient can execute actions and can be switched to synchronous mode;
- `SelectionSet` exposes the active project and current `StorableObject[]` selection;
- `StorableObject` identifiers are session-scoped and become invalid across project close/reopen;
- `TransactionManager` provides read-write transactions;
- `SafetyPoint` rolls changes back unless committed;
- `UndoManager` / `UndoStep` are available;
- EPLAN 2.9 API development targets .NET Framework 4.7.2.
