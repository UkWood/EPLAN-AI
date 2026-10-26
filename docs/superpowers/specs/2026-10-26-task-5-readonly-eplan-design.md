# Task 5 Read-only EPLAN Inspection Design

**Date:** 2026-10-26  
**Status:** approved by existing EPLAN AI Bridge 0.1 design + user instruction to execute next stage  
**Tracks:** Issue #12

## Intent

Implement Task 5 from the approved 0.1 plan: the EPLAN add-in gains the first real DataModel readers while remaining strictly read-only. The result must expose a compact active-project snapshot, page list, current SelectionSet, and whitelisted Function inspection through the existing fixed `AudeuAiDispatch` operation registry.

## Goals

- Return the active project and an ephemeral project-session GUID.
- Keep the same session GUID while the active project database identifier is unchanged; replace it when the database identifier changes.
- List pages compactly.
- Return current SelectionSet as compact summaries plus `ObjectReference` values.
- Resolve an `ObjectReference` only when its session and database identifier still match the active project.
- Inspect only a small Function whitelist: object kind/reference, function name, page name, manual function text and identifying/displayed device tag when available.
- Return stable bridge errors for no project, stale session, missing/invalid object and disallowed object type.

## Non-goals

- No writes, locks, transactions, SafetyPoint or Undo.
- No search across the project.
- No arbitrary EPLAN property ID input.
- No whole-object or whole-property-list serialization.
- No MCP/LegacyHost work yet; those remain later tasks.

## Constraints

- EPLAN-facing code remains `net472` and uses the verified EPLAN 2.9.4 API.
- `SelectionSet.GetCurrentProject(false)` is used for read-only access; selection reads use `SelectionSet.Selection`.
- `Project.DatabaseIdentifier` is the project-session discriminator because EPLAN documents it as unique among open projects and changed after close/reopen.
- `StorableObject.ToStringIdentifier()` is kept only as an ephemeral resolver token; EPLAN documents it as runtime-only and invalid after project close.
- Page enumeration uses `Project.Pages`; page output is ordinal + name + page type name.
- `ObjectReference` remains the shared contract already defined by Task 2.
- `DataJson` stays compact lower-camel JSON and never contains raw EPLAN API objects.

## Approaches considered

### 1. Put DataModel calls directly in `OperationRegistry`
Smallest file count, but session tracking, stale-reference policy and JSON projection become coupled to operation registration and are difficult to unit-test without broad EPLAN stubs.

### 2. Thin EPLAN adapter + read service **(selected)**
`EplanReadAdapter` owns only EPLAN API calls and projects them into small internal snapshots. `ProjectSessionTracker` owns the database-id → session policy. `ReadOperationService` owns operation semantics, validation and response JSON. This keeps EPLAN-specific code narrow while allowing deterministic CI tests around session/error behavior.

### 3. Expand shared Contracts with many DTOs
Would make transport strongly typed, but Task 5 does not need new public wire contracts and this would unnecessarily freeze internal EPLAN projection shapes. Deferred unless later MCP work proves it useful.

## Selected structure

- `ReadModel/ProjectSessionTracker.cs` — current database identifier/session GUID state.
- `ReadModel/ReadSnapshots.cs` — internal project/page/object/function snapshots only.
- `ReadModel/IEplanReadAdapter.cs` — adapter boundary used by tests and service.
- `ReadModel/EplanReadAdapter.cs` — real `SelectionSet`, `Project`, `Page`, `StorableObject`, `Function` access.
- `ReadModel/ReadOperationService.cs` — handles `project.getActive`, `project.listPages`, `selection.get`, `object.get` and stable errors.
- `Serialization/CompactJson.cs` — BCL DataContract JSON helper for lower-camel internal DTOs.
- `OperationRegistry` delegates the four new fixed operations to one long-lived `ReadOperationService` instance.

## Object-reference policy

Creating a reference captures:

- current `projectSessionId`;
- `DatabaseIdentifier`;
- `TypeIdentifier`;
- `ObjectIdentifier`;
- `ToStringIdentifier()`.

Resolution fails closed in this order:

1. no active project → `NO_ACTIVE_PROJECT`;
2. request session missing/mismatch → `SESSION_STALE`;
3. reference database identifier differs from active project → `SESSION_STALE`;
4. identifier cannot resolve → `OBJECT_NOT_FOUND`;
5. resolved object is invalid or identifiers do not match → `OBJECT_INVALID`;
6. `object.get` target is not a Function → `OBJECT_TYPE_NOT_ALLOWED`.

## Operation inputs/outputs

### `project.getActive`
No session input required. Returns `projectSessionId`, `databaseIdentifier`, `name`, `fullName`, `pageCount`, `isReadOnly`.

### `project.listPages`
Requires `ProjectSessionId` matching the current session. Returns `projectSessionId` and ordered page items `{ ordinal, name, pageType }`.

### `selection.get`
Requires current session. Empty selection returns `items: []`. Each item returns `{ kind, reference, name, page }`; Function items include Function name/page, other types use EPLAN type name only.

### `object.get`
Requires current session plus string arguments copied from `ObjectReference`: `databaseIdentifier`, `typeIdentifier`, `objectIdentifier`, `stringIdentifier`. Only Function is allowed in Task 5. Returns `{ kind:"Function", reference, name, page, functionText, deviceTag }`.

## Real-workstation validation

Task 5 has two workstation probes:

1. `scripts/verify-task5-build.ps1` compiles the production AddIn against the actual installed EPLAN 2.9.4 assemblies. This is the compatibility gate for the exact `DataModel` / `HEServices` members used by `EplanReadAdapter`.
2. `scripts/test-task5-active-project.ps1` performs a real `project.getActive` request through `AudeuAiDispatch`. It starts EPLAN with `/UseLastOpenedProjects:1`, so a previously opened safe test project can become active during the `/Auto` run.

The CLI probe deliberately does **not** chain `project.listPages`, `selection.get` and `object.get` across separate `/Auto` runs. `projectSessionId` is intentionally scoped to one live EPLAN/AddIn runtime; every clean `/Auto` invocation creates a new runtime, so reusing the previous session ID would correctly produce `SESSION_STALE`.

Session reuse/staleness, page projection, selection projection and Function-only object inspection are therefore proven deterministically in Task 5 CI. Their persistent end-to-end runtime chain is validated in Task 6 when `LegacyHost` / `EplanRemoteClient` keeps the same EPLAN process alive across multiple bridge calls. This preserves the security model instead of weakening session identity for a test harness.
