# EPLAN AI Bridge 0.1 Implementation Plan

**Status:** approved  
**Date:** 2026-10-23  
**Spec:** `docs/design/eplan-ai-bridge-v0.1.md`

## Goal

Build a local MCP-to-EPLAN 2.9 vertical prototype that can inspect the active project/selection and safely preview/apply one Function Text change.

## Architecture

A .NET 8 stdio MCP server talks over a current-user-only named pipe to a .NET Framework 4.7.2 LegacyHost. LegacyHost uses EPLAN RemoteClient to invoke one fixed EPLAN action, while request/response payloads travel through a GUID-keyed local spool. All EPLAN DataModel access remains inside the EPLAN add-in.

## Global constraints

- Target EPLAN Platform 2.9 for v0.1.
- EPLAN-facing projects target .NET Framework 4.7.2.
- MCP project targets .NET 8.
- Shared contracts target .NET Standard 2.0.
- MCP SDK: `ModelContextProtocol` 2.3.0, pinned.
- EPLAN action name: `AudeuAiDispatch`.
- Pipe name: `Audeu.EplanAi.LegacyHost.v1`.
- Protocol version: `1.0`.
- No TCP/HTTP listener.
- No arbitrary EPLAN Action execution.
- No arbitrary EPLAN property ID exposed through MCP.
- No arbitrary filesystem path accepted from MCP.
- All writes require an immutable previewed change plan.
- Object references are valid only inside the current project session.
- Integration tests use a disposable EPLAN test project.
- Do not start write functionality until read-only M0 succeeds against real EPLAN 2.9.

## Target repository structure

```text
EPLAN-AI/
  EplanAiBridge.sln
  Directory.Build.props
  Directory.Packages.props
  .editorconfig
  .gitignore

  docs/
    design/
    plans/
    decisions/
    setup/
    PROJECT_STATE.md

  src/
    Audeu.EplanAi.Contracts/
    Audeu.EplanAi.LegacyHost/
    Audeu.EplanAi.AddIn/
    Audeu.EplanAi.Mcp/

  tests/
    Audeu.EplanAi.Contracts.Tests/
    Audeu.EplanAi.LegacyHost.Tests/
    Audeu.EplanAi.Mcp.Tests/
    Audeu.EplanAi.AddIn.IntegrationTests/
```

## Review focus

1. Stale EPLAN references after project close/reopen must fail with `SESSION_STALE`, never mutate a newly opened project.
2. Concurrent MCP requests must be serialized by LegacyHost with correct request/response pairing.
3. Malformed or oversized spool payloads must be rejected before EPLAN execution; path traversal must be impossible.
4. If EPLAN starts an internal transaction that undermines expected atomicity, the bridge must report failure instead of claiming success.
5. A Change Plan may be applied at most once and only while its project session remains current.

---

## Task 1 — Prerequisite probe and solution scaffold

Create the solution/project skeleton and document the target EPLAN workstation requirements.

### Deliverables
- `EplanAiBridge.sln`
- `Directory.Build.props`
- `Directory.Packages.props`
- `docs/setup/eplan-2.9-prerequisites.md`
- project files for Contracts, LegacyHost, AddIn, MCP and tests.

### Required behavior
- `EplanApi29Dir` is the only configured local EPLAN API assembly root.
- AddIn/LegacyHost builds fail early with a clear message if required API assemblies are unavailable.
- Framework targets:
  - Contracts: `netstandard2.0`
  - LegacyHost: `net472`
  - AddIn: `net472`
  - MCP: `net8.0`
  - normal tests: `net8.0`
  - EPLAN integration tests: `net472`

### Gate
Before proceeding, verify on the target workstation:
- exact EPLAN 2.9 build;
- API/API Extension entitlement;
- API assembly paths;
- ability to load a minimal add-in;
- RemoteClient availability.

Suggested commit: `build: scaffold EPLAN AI Bridge solution`

---

## Task 2 — Versioned bridge contracts

Create `Audeu.EplanAi.Contracts` with no EPLAN dependency.

### Public contracts
- `ProtocolConstants.Version = "1.0"`
- `BridgeRequest`
- `BridgeResponse`
- `BridgeErrorCodes`
- `ObjectReference`
- `ChangePlan`
- `ChangeOperation`

### Required tests
- serialization round-trip;
- request ID preservation;
- protocol mismatch detection;
- default empty arguments/warnings;
- immutable Change Plan fields;
- allowed transitions `previewed -> applied|failed`;
- double apply forbidden.

Suggested commit: `feat: add versioned bridge contracts`

---

## Task 3 — Secure local spool

Create the GUID-addressed request/response spool used between LegacyHost and the EPLAN add-in.

### Contract
```text
%LOCALAPPDATA%\Audeu\EplanAiBridge\
  requests\<guid>.request.json
  responses\<guid>.response.json
```

### Required behavior
- paths generated internally from canonical GUID only;
- atomic temporary-write + rename;
- request limit 1 MiB;
- response limit 16 MiB;
- cleanup files older than 24 hours;
- no caller-controlled filesystem path.

Suggested commit: `feat: add secure bridge spool`

---

## Task 4 — EPLAN add-in and fixed dispatch action

Create the first real EPLAN component.

### Deliverables
- `EplanAddIn : IEplAddIn`
- `AudeuAiDispatchAction : IEplAction`
- `RequestDispatcher`
- `OperationRegistry`

### Fixed interface
- action name: `AudeuAiDispatch`
- action parameter: `REQUESTID`
- `REQUESTID` must be a valid GUID.
- unknown bridge operation => `BRIDGE_UNKNOWN_OPERATION`.
- bridge operation names are never forwarded as EPLAN action names.

### First checkpoint
A real EPLAN 2.9 instance loads the add-in and a `system.status` request can make a complete action/spool round-trip.

Suggested commit: `feat: register EPLAN AI dispatch action`

---

## Task 5 — Project session and read-only EPLAN operations

Implement the first DataModel readers.

### Required interfaces
- get active project;
- generate/return ephemeral project session ID;
- list pages;
- read current `SelectionSet`;
- create compact `ObjectReference`;
- resolve only current-session references;
- inspect a whitelisted set of Function fields.

### Required behavior
- no project => `NO_ACTIVE_PROJECT`;
- project close/reopen invalidates previous references;
- stale reference => `SESSION_STALE`;
- empty EPLAN selection => empty list, not error;
- never serialize whole EPLAN DataModel objects.

Suggested commit: `feat: add read-only EPLAN project inspection`

---

## Task 6 — LegacyHost RemoteClient gateway

Create the EPLAN Remoting adapter.

### Required operations
- discover running EPLAN servers;
- connect/disconnect;
- ping;
- execute one bridge request;
- synchronous action execution;
- timeout mapping to `OPERATION_TIMEOUT`.

The outgoing action invocation may contain only the fixed `AudeuAiDispatch` action and generated request GUID.

Suggested commit: `feat: add EPLAN RemoteClient gateway`

---

## Task 7 — Current-user named-pipe LegacyHost

Expose LegacyHost only to the current Windows user.

### Requirements
- pipe: `Audeu.EplanAi.LegacyHost.v1`;
- Windows ACL restricted to current user SID;
- incoming message maximum 1 MiB;
- one EPLAN request in flight at a time;
- concurrent MCP requests serialized;
- graceful RemoteClient shutdown.

Suggested commit: `feat: expose local secured LegacyHost pipe`

---

## Task 8 — MCP read-only tools

Build the .NET 8 stdio MCP server and expose:

- `eplan_status`
- `eplan_get_active_project`
- `eplan_list_pages`
- `eplan_get_selection`
- `eplan_get_object`

### Requirements
- MCP process references no EPLAN assembly;
- compact JSON results only;
- 10-second named-pipe connection timeout;
- stable bridge error mapping;
- no raw EPLAN types in MCP tool schemas.

Suggested commit: `feat: expose read-only EPLAN MCP tools`

---

## Task 9 — M0 read-only acceptance gate

This is the mandatory gate before any write functionality.

### Manual/integration acceptance
Using a disposable EPLAN project:
1. EPLAN is detected.
2. Active project is returned.
3. Page count/list is correct.
4. A selected Function is returned.
5. Function inspection returns expected Function Text.
6. Close/reopen invalidates the old object reference and returns `SESSION_STALE`.

Record repeatable test instructions in `docs/setup/local-integration-test.md`.

Suggested commit: `test: add M0 EPLAN bridge acceptance gate`

---

## Task 10 — MCP Change Plan state machine

Create an in-memory immutable Change Plan store.

### First supported plan
`setFunctionText`

### Rules
- preview may read EPLAN but may not write;
- plan is bound to project session;
- plan stores exact target, old value, new value;
- plan cannot be modified after creation;
- plan cannot be applied twice;
- plan is not persisted across MCP process restart in v0.1;
- initial proposed text limit: 1024 UTF-16 characters, reduced if actual EPLAN field limits require it.

Suggested commit: `feat: add immutable EPLAN change plans`

---

## Task 11 — Safe Function Text preview/apply in EPLAN

Implement the only write operation allowed in 0.1.

### Apply sequence
```text
validate plan/session
  -> resolve Function
  -> capture old value
  -> create UndoStep
  -> create SafetyPoint
  -> create Transaction
  -> set Function Text
  -> read value back
  -> verify post-condition
  -> Transaction.Commit
  -> SafetyPoint.Commit
```

### Tests
- preview does not modify project;
- non-Function target => `OBJECT_TYPE_NOT_ALLOWED`;
- stale target => `SESSION_STALE`;
- controlled failure after assignment but before commit leaves original value intact;
- successful apply can be verified by reading EPLAN again;
- undo-last exposed only while valid for the current session.

Suggested commit: `feat: safely apply Function Text change`

---

## Task 12 — MCP apply/undo and M1 acceptance

Expose:
- `eplan_preview_set_function_text`
- `eplan_apply_change_plan`
- `eplan_undo_last_ai_change`

### Acceptance sequence
1. open disposable project;
2. select Function;
3. inspect it;
4. preview text change;
5. confirm EPLAN remains unchanged;
6. apply the stored plan;
7. confirm new text in EPLAN and through MCP;
8. undo;
9. confirm original text;
10. repeat with injected failure and confirm rollback.

Suggested commit: `feat: complete EPLAN AI Bridge 0.1 write flow`

---

## Task 13 — Final verification and v0.1 packaging

### Deliverables
- installation documentation;
- troubleshooting documentation;
- packaged add-in;
- packaged LegacyHost;
- packaged MCP server.

### Final verification
- all non-EPLAN unit tests pass;
- AddIn and LegacyHost compile against verified EPLAN 2.9 assemblies;
- M0 and M1 integration acceptance pass;
- security review confirms:
  - no arbitrary action forwarding;
  - no arbitrary property ID input;
  - no TCP listener;
  - no model-controlled arbitrary path;
  - no arbitrary code execution.

Release tag: `eplan-ai-bridge-v0.1.0`.

---

## Milestones and gates

### Gate A — environment validated
Task 1 complete. If API licensing/assemblies are unavailable, stop and re-scope instead of silently switching to GUI automation.

### Gate B — M0 read-only bridge proven
Tasks 2–9 complete. This is mandatory before write work.

### Gate C — M1 safe write proven
Tasks 10–12 complete.

### Gate D — v0.1 release
Task 13 complete.

## Execution policy

Tasks 1–4 are executed sequentially because EPLAN API/environment discoveries can change exact references and project setup. After the real dispatch path is proven, subsequent tasks remain small, independently reviewed increments.

The immediate implementation checkpoint is **Task 4**: EPLAN 2.9 loads the add-in and `AudeuAiDispatch` successfully round-trips a `system.status` request.
