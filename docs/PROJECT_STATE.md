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
- Task 5 AddIn compiles against the real EPLAN 2.9.4 API assemblies;
- EPLAN loads the AddIn through `%APPDATA%\EPLAN\ShadowCopyAssemblies`;
- real lifecycle observed: `OnInit -> OnInitGui -> OnExit`;
- real `AudeuAiDispatchAction` construction and `IEplAction.OnRegister()` observed;
- action registered as `AudeuAiDispatch`, ordinal `20`;
- real `system.status` request/response spool round-trip PASS;
- real `project.getActive` request/response spool round-trip PASS: session `e4caa3c7-e888-47be-ae0c-42b51403df9f`, database ID `25`, project `УСПД Жуковский ПНС`, page count `7`, read-only `False`.

Gate A status:

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime portion:** PASS for build, module load, action registration, action execution, request read, dispatch and response write.
- **Gate A2 — Task 5 DataModel compatibility:** PASS on real EPLAN 2.9.4.
- **Gate A2 — RemoteClient portion:** pending Task 6.

Gate B / M0 still requires LegacyHost/RemoteClient, named pipe, MCP read tools and final read-only acceptance.

## Current status

- [x] Repository/foundation documentation established.
- [x] Architecture 0.1 approved.
- [x] Sequential M0 -> M1 plan approved.
- [x] Gate A1 validated.
- [x] Task 1: repository scaffold and CI — branch `m0/task-1-scaffold`, draft PR #3.
- [x] Task 2: versioned bridge contracts — branch `m0/task-2-contracts`, issue #4 closed, draft PR #5.
- [x] Task 3: secure local spool — branch `m0/task-3-spool`, issue #6 closed, draft PR #7.
- [x] Task 4: minimal AddIn + fixed `AudeuAiDispatch` — branch `m0/task-4-dispatch`, Issue #8 completed, draft PR #11.
- [x] Task 5: project session + read-only EPLAN operations — real EPLAN build and `project.getActive` acceptance PASS. Branch `m0/task-5-readonly-inspection`, Issue #12 completed, draft PR #15.
- [ ] Task 6: LegacyHost RemoteClient gateway.
- [ ] Task 7: current-user named-pipe LegacyHost.
- [ ] Task 8: MCP read-only tools.
- [ ] Task 9 / Gate B: prove complete read-only bridge on real EPLAN 2.9.
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

Evidence: Contracts **18/18**, LegacyHost spool **8/8**, final Task 3 CI `39976839719` success.

## Task 4 — minimal AddIn and fixed dispatch action

Canonical tracking: Issue #8. Branch `m0/task-4-dispatch`. Draft stacked PR #11 remains unmerged.

Implemented fixed `AudeuAiDispatch`, secure request-ID dispatch, whitelisted `system.status`, diagnostic lifecycle/action logging and BOM-tolerant request reading.

Final repo evidence: CI `40303614597`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **13/13**, MCP build and prerequisite guards success.

Real EPLAN acceptance: `system.status` round-trip PASS with `Execute:entered`, matching request ID and `Execute:success`.

## Task 5 — project session and read-only EPLAN inspection

Canonical tracking: Issue #12. Branch `m0/task-5-readonly-inspection`. Draft stacked PR #15 targets Task 4 and remains unmerged.

Implemented fixed read operations:

- `project.getActive`;
- `project.listPages`;
- `selection.get`;
- `object.get` (Function-only in Task 5).

Session/reference policy fails closed on project/session changes and never exposes arbitrary EPLAN property IDs or whole DataModel objects.

Repo-side evidence:

- session tracker RED `40306260040`, GREEN `40306678839` (AddIn **17/17**);
- read-service RED `40307026719`, GREEN `40308065557` (AddIn **27/27**);
- real-adapter RED `40308440298`;
- final repo GREEN `40309525419`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **31/31**, MCP build and prerequisite guards success.

### Task 5 real-workstation acceptance — PASS

Real production build against `C:\Program Files\EPLAN\Platform\2.9.4\Bin`:

```text
PASS: Task 5 AddIn compiled against real EPLAN 2.9.4 assemblies
```

Real `project.getActive` round-trip:

```text
PASS: Task 5 project.getActive succeeded for request 181d5d4a-9818-49e0-843a-327c27a642c4
projectSessionId : e4caa3c7-e888-47be-ae0c-42b51403df9f
databaseId       : 25
name             : УСПД Жуковский ПНС
fullName         : E:\EPLAN\УСПД Жуковский ПНС
pageCount        : 7
isReadOnly       : False
```

This proves the real EPLAN path for the first DataModel read. Persistent-session validation of `project.listPages`, `selection.get` and `object.get` moves to Task 6, where RemoteClient keeps one EPLAN runtime alive.

## Immediate next actions

1. Do not merge PR #3, #5, #7, #11 or #15 without explicit user approval.
2. Start Task 6 from the validated Task 5 head: LegacyHost RemoteClient gateway.
3. Task 6 must discover running EPLAN servers, connect/disconnect, ping, force synchronous action execution, execute only `AudeuAiDispatch /REQUESTID:<guid>`, and map timeout to `OPERATION_TIMEOUT`.
4. Use the persistent EPLAN connection to validate `project.getActive -> project.listPages -> selection.get -> object.get` in one runtime.
5. Task 7 will then expose LegacyHost through the current-user-only named pipe.
6. Do not start M1 write work until Gate B / M0 passes on real EPLAN 2.9.

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
