# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-26

## Purpose

This is the living restart point for the project. Update it after every meaningful implementation step, architectural decision, blocker, test result, or milestone change.

## Approved architecture

```text
ChatGPT / MCP client
        |
        v
Audeu.EplanAi.Mcp            (.NET 8)
        |
        | current-user Named Pipe
        v
Audeu.EplanAi.LegacyHost     (.NET Framework 4.7.2)
        |
        | EPLAN Remoting / WCF
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

Key rule: the LLM never receives unrestricted EPLAN API access. It works through a small versioned bridge/domain API with explicit validation and Change Plans.

## Approved documents

- Technical design: `docs/design/eplan-ai-bridge-v0.1.md`
- Implementation plan: `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- Architecture decision: `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- Gate A procedure: `docs/setup/eplan-2.9-prerequisites.md`
- Task 4 local validation: `docs/setup/task4-local-validation.md`
- Task 5 design: `docs/superpowers/specs/2026-10-26-task-5-readonly-eplan-design.md`
- Task 5 implementation plan: `docs/superpowers/plans/2026-10-26-task-5-readonly-eplan.md`
- Task 5 local validation: `docs/setup/task5-local-validation.md`

## Verified target workstation

Confirmed on 2026-10-23 and extended with real build/runtime evidence on 2026-10-26:

- EPLAN Electric P8 Professional 64-bit;
- version 2.9 SP1 Update 4, build 14773;
- EPLAN API / API Extension available;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- required AddIn/API DLLs present;
- required Remoting DLLs present;
- .NET SDK x64 available under `C:\Program Files\dotnet` (SDK 8.0.427 confirmed);
- Task 4 AddIn compiles against the real EPLAN 2.9.4 API assemblies;
- EPLAN loads the AddIn through `%APPDATA%\EPLAN\ShadowCopyAssemblies`;
- real lifecycle observed: `OnInit -> OnInitGui -> OnExit`;
- real `AudeuAiDispatchAction` construction and `IEplAction.OnRegister()` observed;
- action registered as `AudeuAiDispatch`, ordinal `20`;
- real `system.status` request/response spool round-trip PASS on 2026-10-26.

Gate A status:

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime portion:** PASS for Task 4 build, module load, action registration, action execution, request read, dispatch and response write.
- **Gate A2 — Task 5 DataModel compatibility:** pending real workstation build against EPLAN 2.9.4 assemblies.
- **Gate A2 — RemoteClient portion:** pending Task 6.

Gate B / M0 still requires Task 5 real-workstation validation and the LegacyHost/RemoteClient path.

## Current status

- [x] Repository/foundation documentation established.
- [x] Architecture 0.1 approved.
- [x] Sequential M0 -> M1 plan approved.
- [x] Gate A1 validated.
- [x] Task 1: repository scaffold and CI — branch `m0/task-1-scaffold`, draft PR #3.
- [x] Task 2: versioned bridge contracts — branch `m0/task-2-contracts`, issue #4 closed, draft PR #5.
- [x] Task 3: secure local spool — branch `m0/task-3-spool`, issue #6 closed, draft PR #7.
- [x] Task 4: minimal AddIn + fixed `AudeuAiDispatch` — branch `m0/task-4-dispatch`, Issue #8 completed, draft PR #11.
- [~] Task 5: project session + read-only EPLAN operations — repo-side implementation and CI PASS; real EPLAN build + `project.getActive` smoke pending. Branch `m0/task-5-readonly-inspection`, Issue #12, draft PR #15.
- [ ] Task 6: LegacyHost RemoteClient gateway.
- [ ] Gate B / M0: prove complete read-only bridge on real EPLAN 2.9.
- [ ] Gate C / M1: prove safe Function Text Preview -> Apply -> Verify -> Undo/Rollback.

## Test runner ruling

xUnit v3 test projects are executable test applications. CI completion evidence must contain non-zero executed-test counts; a green step without an executed-test summary is insufficient.

## Task 1 — scaffold

Implemented solution/project shells, shared MSBuild configuration, central packages, EPLAN API path guards, MCP/Contracts shells and Windows CI.

Evidence: CI `39959463954` success.

## Task 2 — contracts

Implemented protocol `1.0`, `BridgeRequest` / `BridgeResponse`, session-scoped `ObjectReference`, stable error codes, and initial Change Plan contracts.

Evidence: CI `39975775380` success; Contracts **18/18**.

## Task 3 — secure spool

Implemented fixed root `%LOCALAPPDATA%\Audeu\EplanAiBridge`, GUID request/response names, request/response size limits, temp + same-directory rename, duplicate fail-closed behavior, response ID verification and stale cleanup.

Evidence:

- RED `39975854174`;
- implementation commit `08e386f9e690dc8dece51c96a20b659dd49c0512`;
- synchronized branch CI `39976839719` success;
- PR CI `39976847095` success;
- Contracts **18/18**;
- LegacyHost spool **8/8**.

## Task 4 — minimal AddIn and fixed dispatch action

Canonical tracking: Issue #8. Branch `m0/task-4-dispatch`. Draft stacked PR #11 remains unmerged.

Implemented:

- exactly one `EplanAddIn : IEplAddIn`;
- exactly one bridge `AudeuAiDispatchAction : IEplAction`;
- fixed action `AudeuAiDispatch`, ordinal `20`;
- only canonical GUID `REQUESTID` accepted;
- fixed local spool paths only;
- explicit operation whitelist;
- first operation `system.status`;
- stable protocol mismatch/unknown operation errors;
- valid request processing exceptions surface as `INTERNAL_ERROR` responses;
- diagnostic lifecycle/action logs for real-workstation validation;
- UTF-8 BOM-tolerant request reader to handle Windows PowerShell 5.1 generated requests;
- validation harness writes UTF-8 without BOM and reads responses explicitly as UTF-8.

### Task 4 important real-API findings

1. Real EPLAN 2.9.4 `ActionProperties` does not expose `Description`, despite an EPLAN documentation example using it. CI stub was corrected; `GetActionProperties(ref ActionProperties)` is a no-op.
2. `IEplAddIn.OnRegister()` is not expected to repeat for an already registered module. Real load proof is `OnInit/OnInitGui/OnExit` plus ShadowCopy location.
3. Windows PowerShell 5.1 `Set-Content -Encoding UTF8` adds a BOM. Direct `DataContractJsonSerializer.ReadObject()` on the raw stream rejected it. Regression coverage now proves BOM-prefixed requests are accepted.

### Task 4 final evidence

- UTF-8 BOM GREEN CI `40303614597`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **13/13**, MCP build and prerequisite guards success.
- Final real request: `ea766505-2090-4c57-b931-4063bb03ae28`.
- Real EPLAN output: `PASS: AudeuAiDispatch system.status round-trip succeeded`.
- Diagnostics: `Execute:entered`, matching request ID, `Execute:success`.

Task 4 is complete; Issue #8 is closed. PR #11 remains draft/unmerged.

## Task 5 — project session and read-only EPLAN inspection

Canonical tracking: Issue #12. Branch `m0/task-5-readonly-inspection`. Draft stacked PR #15 targets Task 4 and remains unmerged.

### Task 5 fixed operation surface

The AddIn operation registry now exposes only these Task 5 reads in addition to `system.status`:

- `project.getActive`;
- `project.listPages`;
- `selection.get`;
- `object.get`.

No write operation, arbitrary EPLAN action name, arbitrary property ID, lock, transaction, SafetyPoint or Undo path was introduced.

### Task 5 architecture

- `ProjectSessionTracker` owns ephemeral `DatabaseIdentifier -> Guid` project session identity.
- `IEplanReadAdapter` separates EPLAN API calls from deterministic service semantics.
- `EplanReadAdapter` is the only Task 5 DataModel/HEServices adapter.
- `ReadOperationService` validates sessions/references and projects compact response JSON.
- `CompactJson` serializes internal lower-camel result DTOs.
- one long-lived `ReadOperationService` is held by `OperationRegistry` for the lifetime of the AddIn runtime.

### Task 5 session/reference policy

- same active `DatabaseIdentifier` in one runtime -> same session GUID;
- database identifier change -> new session GUID and old session becomes stale;
- no active project -> `NO_ACTIVE_PROJECT` and tracker clears;
- missing/mismatched project session -> `SESSION_STALE`;
- object reference database mismatch -> `SESSION_STALE`;
- malformed object reference arguments -> `VALIDATION_FAILED`;
- unresolved identifier -> `OBJECT_NOT_FOUND`;
- resolved identity mismatch/invalid object -> `OBJECT_INVALID`;
- non-Function `object.get` -> `OBJECT_TYPE_NOT_ALLOWED`;
- `StringIdentifier` remains runtime-only and is never treated as durable identity.

### Task 5 EPLAN adapter reads

`EplanReadAdapter` uses only read APIs:

- `SelectionSet.GetCurrentProject(false)`;
- active `Project` identity/name/path/read-only state;
- `Project.Pages`;
- `SelectionSet.Selection`;
- `StorableObject` database/type/object/string identifiers and validity/type name;
- `StorableObject.FromStringIdentifier()` for runtime-scoped resolution;
- Function whitelist: name, page, `FUNC_TEXT`, `FUNC_IDENTDEVICETAG`.

### Task 5 TDD / CI evidence

Session tracker:

- RED commit `b86b2b3bdd45609316fdcde842e8790ffa143c36`, CI `40306260040`: AddIn compile failed because tracker/constants were absent after Contracts **18/18** and spool **8/8**;
- GREEN CI `40306678839`: AddIn **17/17**, full workflow success.

Read-service semantics:

- RED commit `f6cd872cbd4e84961ea1f260878d93913fbdb810`, CI `40307026719`: compile failed because service/snapshots/adapter boundary were absent;
- first implementation CI exposed one test-only JSON escaping assumption (`/` represented as `\/`);
- diagnostic CI `40307755587` identified the exact assertion;
- corrected test commit `03311b8941aa8e7c58f35b0b8b6ed20d6e663db4`;
- GREEN CI `40308065557`: AddIn **27/27**, full workflow success.

Real adapter / registry:

- RED commit `dfa011296489bb4b11c6936965990bf802c74c38`, CI `40308440298`: Contracts **18/18**, spool **8/8**, then AddIn compile failed only because `EplanReadAdapter` was absent;
- production adapter commit `413b0bd104f3c6f3103993537e4b9c7e53aeff7b`;
- operation registry commit `efac5e89f5085ce321baa71755ee90a3407582f2`;
- GREEN push CI `40308748325`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **31/31**, MCP build success, AddIn and LegacyHost prerequisite guards success.

### Task 5 real-workstation validation — pending

Two scripts are provided:

```powershell
.\scripts\verify-task5-build.ps1
.\scripts\test-task5-active-project.ps1
```

The build probe must compile the current production AddIn against the actual workstation EPLAN 2.9.4 assemblies. This may expose an exact API signature mismatch; if so, treat the real installed assembly as authoritative and fix via regression TDD before proceeding.

The runtime smoke probe writes a UTF-8-no-BOM `project.getActive` request and launches EPLAN with `/UseLastOpenedProjects:1`. Expected success includes a non-empty `projectSessionId`, non-zero `databaseIdentifier`, project name/path, page count and read-only state.

`project.listPages`, `selection.get` and `object.get` are intentionally **not** chained across separate `/Auto` CLI invocations: each clean command-line invocation starts a new AddIn runtime, while `projectSessionId` is intentionally runtime-scoped. Reusing an old session in a new process should fail with `SESSION_STALE`. Full persistent-runtime validation of these operations belongs to Task 6 through `LegacyHost` / `EplanRemoteClient`.

Task 5 remains open until the real build and `project.getActive` smoke probe pass.

## Immediate next actions

1. Do not merge PR #3, #5, #7, #11 or #15 without explicit user approval.
2. On the verified EPLAN workstation, switch/pull `m0/task-5-readonly-inspection`.
3. Run `scripts\verify-task5-build.ps1` and preserve the full output.
4. If the build passes, ensure a safe test project has been opened once and EPLAN is closed normally.
5. Run `scripts\test-task5-active-project.ps1`; if the License Manager appears, confirm the existing license selection as in Task 4.
6. If both probes pass, record evidence, close Issue #12 and then start Task 6.
7. If the real build fails on an API member/signature, stop and fix that exact mismatch before Task 6.
8. Do not start M1 write work until Gate B / M0 passes on real EPLAN 2.9.

## Release 0.1 boundaries

Included:
- connection/status;
- active project;
- page list;
- current selection;
- selected Function inspection;
- preview of one Function Text change;
- explicit apply;
- read-back verification;
- rollback/undo behavior.

Deferred:
- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- equipment selection;
- arbitrary EPLAN property/action access.

## Working rules

1. Keep architecture, plans, decisions, code and test evidence in this repository.
2. Update this file after every milestone or material blocker.
3. Prefer small branches/PRs with independently verifiable outcomes.
4. Never test mutating operations on a production EPLAN project.
5. Do not start M1 write work until M0 passes on real EPLAN 2.9.
6. No arbitrary code, property ID, EPLAN action name, or filesystem path may be model-controlled.
7. Durable EPLAN object identity is never inferred from session-scoped `DatabaseIdentifier`/`ObjectIdentifier` values.
8. A CI step is not proof that tests ran unless the runner reports a non-zero executed-test count.
9. Do not merge project PRs without explicit user approval.
