# Task 5 Read-only EPLAN Inspection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic read-only active-project, page, selection and Function inspection operations to the proven EPLAN add-in dispatch path.

**Architecture:** Keep EPLAN API access inside a thin `EplanReadAdapter`; keep session/stale-reference policy in testable read-model services; register only four fixed read operation names in `OperationRegistry`. Responses remain compact JSON strings in the existing bridge envelope.

**Tech Stack:** C#; .NET Framework 4.7.2; EPLAN 2.9.4 DataModel/HEServices; xUnit v3/Microsoft Testing Platform; DataContractJsonSerializer.

**Spec:** `docs/superpowers/specs/2026-10-26-task-5-readonly-eplan-design.md`

## Global Constraints

- No write API calls or object locks.
- No arbitrary property IDs, EPLAN action names or filesystem paths.
- Session identity changes when active `DatabaseIdentifier` changes.
- Runtime-only `StringIdentifier` is never treated as durable identity.
- `object.get` supports Function only in Task 5.
- Empty selection is success with an empty array.
- Production builds still compile against real `EplanApi29Dir`; CI stubs remain test-only.

## Review Focus

1. Active project disappears between calls → `NO_ACTIVE_PROJECT`, never stale cached data.
2. Database ID changes while caller reuses old session → `SESSION_STALE` before object resolution.
3. Malformed object-reference numeric arguments → `VALIDATION_FAILED`, not an exception leak.
4. Empty selection → successful `items: []`.
5. Resolved non-Function object → `OBJECT_TYPE_NOT_ALLOWED`; no arbitrary property serialization.

---

### Task 1: Extend stable read error codes and session tracker

**Files:**
- Modify: `src/Audeu.EplanAi.Contracts/BridgeErrorCodes.cs`
- Create: `src/Audeu.EplanAi.AddIn/ReadModel/ProjectSessionTracker.cs`
- Test: `tests/Audeu.EplanAi.AddIn.IntegrationTests/ProjectSessionTrackerTests.cs`

**Interfaces:**
- Produces: `ProjectSessionTracker.GetOrCreate(uint databaseIdentifier) -> Guid`, `IsCurrent(Guid,uint) -> bool`, `Clear()`.
- Produces error constants: `NO_ACTIVE_PROJECT`, `OBJECT_NOT_FOUND`, `OBJECT_INVALID`, `OBJECT_TYPE_NOT_ALLOWED`, `VALIDATION_FAILED`.

- [ ] Write tracker/error-code tests first.
- [ ] Run AddIn tests and verify RED because tracker/constants are absent.
- [ ] Implement the minimal tracker and constants.
- [ ] Run AddIn tests and verify GREEN.

### Task 2: Define internal snapshots and read-service semantics

**Files:**
- Create: `src/Audeu.EplanAi.AddIn/ReadModel/ReadSnapshots.cs`
- Create: `src/Audeu.EplanAi.AddIn/ReadModel/IEplanReadAdapter.cs`
- Create: `src/Audeu.EplanAi.AddIn/ReadModel/ReadOperationService.cs`
- Create: `src/Audeu.EplanAi.AddIn/Serialization/CompactJson.cs`
- Test: `tests/Audeu.EplanAi.AddIn.IntegrationTests/ReadOperationServiceTests.cs`

**Interfaces:**
- `IEplanReadAdapter.GetActiveProject()` returns null or a compact `ProjectSnapshot`.
- `IEplanReadAdapter.GetPages(uint databaseIdentifier)` returns compact page snapshots.
- `IEplanReadAdapter.GetSelection(uint databaseIdentifier)` returns compact object snapshots carrying `ObjectReference` material.
- `IEplanReadAdapter.ResolveFunction(uint databaseIdentifier, string stringIdentifier)` returns null/non-Function/Function snapshot outcome without exposing EPLAN objects.
- `ReadOperationService.Handle(BridgeRequest)` dispatches exactly `project.getActive`, `project.listPages`, `selection.get`, `object.get`.

- [ ] Write service tests for no project, stable/current session, stale session, page list, empty selection, malformed object args, missing object and non-Function target.
- [ ] Run AddIn tests and verify RED because service/interfaces are absent.
- [ ] Implement minimal snapshots, JSON helper and service.
- [ ] Run AddIn tests and verify GREEN.

### Task 3: Implement real EPLAN read adapter and register fixed operations

**Files:**
- Create: `src/Audeu.EplanAi.AddIn/ReadModel/EplanReadAdapter.cs`
- Modify: `src/Audeu.EplanAi.AddIn/Dispatch/OperationRegistry.cs`
- Modify: `test-support/EplanApiStubs.cs`
- Test: `tests/Audeu.EplanAi.AddIn.IntegrationTests/ReadAdapterContractTests.cs`

**Interfaces:**
- Adapter uses `SelectionSet.GetCurrentProject(false)`, `Project.Pages`, `SelectionSet.Selection`, `StorableObject` identity members and `Function` whitelist only.
- Registry exposes fixed operations `project.getActive`, `project.listPages`, `selection.get`, `object.get` plus existing `system.status`.

- [ ] Expand test-only EPLAN stubs and write adapter/registry tests first; tests must assert fixed operation availability and no write methods are required.
- [ ] Run AddIn tests and verify RED because production adapter/registry wiring is absent.
- [ ] Implement `EplanReadAdapter` and wire one long-lived `ReadOperationService` into `OperationRegistry`.
- [ ] Run AddIn tests and verify GREEN.
- [ ] Run full CI and require non-zero counts for Contracts, LegacyHost spool and AddIn suites.

### Task 4: Add real-workstation Task 5 validation harness and project state

**Files:**
- Create: `scripts/test-task5-readonly.ps1`
- Create/Modify: `docs/setup/task5-local-validation.md`
- Modify: `docs/PROJECT_STATE.md`

**Interfaces:**
- Harness writes canonical UTF-8-no-BOM spool requests and invokes only fixed read operations.
- It reports active-project/page/selection output and provides a selectable Function object-get path when a Function is selected.

- [ ] Add script/docs after repo-side GREEN.
- [ ] Verify script syntax and CI remains GREEN.
- [ ] Record Task 5 repo-side evidence and exact real-workstation commands.
- [ ] Open a stacked draft PR targeting `m0/task-4-dispatch`; do not merge.

## Self-review

- Spec coverage: all Task 5 plan requirements map to Tasks 1–4.
- Type consistency: session is `Guid`; database/type IDs are `uint`; object ID is `long`; string identifier is runtime-only string.
- Security: adapter accepts no arbitrary property ID; registry accepts only fixed operation names; no writes introduced.
- Real API risk: exact EPLAN members are deliberately isolated in `EplanReadAdapter` so workstation compilation can correct any 2.9.4 signature mismatch without changing service semantics.
