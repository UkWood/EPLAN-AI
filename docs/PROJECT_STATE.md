# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-27

## Purpose

Living restart point for the project. GitHub is the durable source of truth for architecture, plans, decisions, implementation state, test evidence and workstation acceptance.

## Approved architecture

```text
ChatGPT / MCP client
        |
        | MCP stdio
        v
Audeu.EplanAi.Mcp            (.NET 8)
        |
        | fixed current-user Named Pipe
        v
Audeu.EplanAi.LegacyHost     (.NET Framework 4.7.2)
        |
        | EPLAN Remoting / fixed AudeuAiDispatch
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

The LLM never receives unrestricted EPLAN API access. MCP exposes a closed-world bridge/domain API only. The MCP process references no EPLAN assembly. LegacyHost is the only process that knows EPLAN RemoteClient. AddIn is the only component that accesses EPLAN DataModel/HEServices.

## Approved documents

- `docs/design/eplan-ai-bridge-v0.1.md`
- `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- `docs/setup/eplan-2.9-prerequisites.md`
- Task 4/5/6/7 setup and design documents already retained in the repository
- `docs/design/task-8-mcp-readonly-tools.md`
- `docs/plans/2026-10-26-task-8-mcp-readonly-tools.md`
- `docs/design/task-9-gate-b-workstation-acceptance.md`
- `docs/plans/2026-10-26-task-9-gate-b-workstation-acceptance.md`
- `docs/setup/task9-gate-b-validation.md`
- `docs/superpowers/specs/2026-10-27-task-10-change-plan-design.md`
- `docs/superpowers/plans/2026-10-27-task-10-change-plan.md`
- `docs/superpowers/specs/2026-10-27-task-11-safe-function-text-design.md`
- `docs/superpowers/plans/2026-10-27-task-11-safe-function-text.md`

## Verified target workstation

Confirmed through real build/runtime evidence on 2026-10-26 and 2026-10-27:

- EPLAN Electric P8 Professional 64-bit;
- version 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting DLLs present;
- x64 .NET SDK 8.0.427 available under `C:\Program Files\dotnet`;
- workstation PATH also contains `C:\Program Files (x86)\dotnet` before x64, so workstation scripts prepend `C:\Program Files\dotnet` to the process PATH;
- AddIn compiles against the real EPLAN 2.9.4 assemblies and loads in EPLAN;
- fixed action `AudeuAiDispatch` registers and executes;
- real RemoteClient discovery/connect/ping succeeds;
- real persistent named-pipe read sequence succeeds against project `УСПД Жуковский ПНС`, database ID `25`, 7 pages;
- real official MCP stdio client reaches EPLAN end-to-end through all bridge layers;
- displayed Function Text `Контроллер УСПД БКД-ПК-RF.2` is returned correctly for selected Function `===+++УСПД_Э3#-A1`;
- closing the active project yields stable `NO_ACTIVE_PROJECT` and clears the old project session;
- reopening the project establishes a fresh session and the old Function reference is rejected with stable `SESSION_STALE`;
- MCP probe exits successfully and LegacyHost performs a clean Ctrl+C shutdown.

## Gate status

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime / real DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly/runtime compatibility:** PASS.
- **Tasks 1–9 / M0 read-only bridge:** **PASS and integrated to `main`**.
- **Task 8 MCP implementation:** merged via PR #21 at `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`; main CI #303 PASS.
- **Gate B / Task 9 workstation acceptance:** **PASS** on real EPLAN 2.9.4; Issue #22 completed; merged via PR #23 at `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`; main CI #304 and PowerShell Encoding Guard #31 PASS.
- **Task 10 MCP Change Plan lifecycle:** **REPO-SIDE PASS** on `m1/task-10-change-plans`; PR #25 is ready but remains unmerged pending explicit approval.
- **Task 11 internal safe Function Text write path:** **REPO-SIDE PASS** on `m1/task-11-safe-function-text`; PR #27 remains unmerged pending explicit approval.
- **Gate C / M1 safe write:** IN PROGRESS. The immutable preview lifecycle and internal AddIn write/undo boundary are implemented and repo-tested, but no mutating MCP tool has been exposed and no real workstation mutation acceptance has been performed yet.

## Current status

- [x] Task 1: repository scaffold / CI.
- [x] Task 2: versioned contracts.
- [x] Task 3: secure local spool.
- [x] Task 4: AddIn + fixed `AudeuAiDispatch`.
- [x] Task 5: project session + read-only EPLAN inspection.
- [x] Task 6: LegacyHost RemoteClient gateway.
- [x] Task 7: secured current-user named-pipe LegacyHost.
- [x] Task 8: .NET 8 stdio MCP read-only tools — merged to `main` via PR #21.
- [x] Task 9 / Gate B: real MCP-to-EPLAN read-only acceptance including stale-session behavior — PASS and merged to `main` via PR #23.
- [x] Task 10: immutable MCP Change Plan state machine for `setFunctionText` — repo-side complete on branch `m1/task-10-change-plans`, PR #25.
- [x] Task 11: internal safe Function Text apply/undo path in AddIn/EPLAN boundary — repo-side complete on branch `m1/task-11-safe-function-text`, PR #27; no real workstation mutation performed.
- [ ] Task 12 / Gate C: MCP preview/apply/undo tools and real M1 acceptance on a disposable/safe EPLAN project.
- [ ] Task 13 / Gate D: final verification and v0.1 packaging.

Tasks 1–7 were integrated to `main` at merge commit `1064c841d0b33f29b649b03c7dd902fd3f4a7bbd`; main CI #239 (`40360507085`) passed after integration.

Tasks 8–9 completed M0 integration on 2026-10-27:

- PR #21 -> `main` merge commit `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`; main CI #303 PASS; Issue #20 completed.
- PR #23 -> `main` merge commit `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`; main CI #304 PASS; PowerShell Encoding Guard #31 PASS; Issue #22 completed.

## Test runner ruling

xUnit v3 test projects are executable Microsoft Testing Platform applications. A CI step is accepted as test evidence only when it reports a non-zero executed-test count.

Task 8 initially exposed a zero-discovery MCP test configuration. That green-looking run was rejected as invalid evidence. `Audeu.EplanAi.Mcp.Tests` was corrected with `OutputType=Exe` and `TestingPlatformDotnetTestSupport=true`; CI #257 then executed and passed **17/17 MCP tests**.

Task 9 later added a dedicated MCP probe regression suite. Final Task 9 runs execute and pass **6/6 MCP probe tests** in addition to the existing suites. Final merged `main` CI #304 passed the full repository pipeline.

Task 10 expands the MCP suite to **38/38 PASS** while preserving non-zero discovery and the existing Gate-B probe suite at **6/6 PASS**. CI #317 also passed Contracts **20/20**, LegacyHost **45/45**, AddIn **32/32**, MCP build, probe build, PowerShell 5.1 parsing and both EPLAN prerequisite guards.

Task 11 expands the AddIn integration suite to **60/60 PASS** while leaving the MCP suite at **38/38 PASS** and the Gate-B probe regressions at **6/6 PASS**. Final code CI #332 also passed Contracts **20/20**, LegacyHost **45/45**, MCP/probe builds, PowerShell 5.1 parsing and both EPLAN prerequisite guards; PowerShell Encoding Guard #59 passed on the same code head.

## Task 4 — fixed AddIn dispatch — PASS

Implemented `EplanAddIn : IEplAddIn`, fixed `AudeuAiDispatchAction : IEplAction`, secure canonical `REQUESTID`, `system.status`, diagnostics and UTF-8/BOM handling.

Real EPLAN `system.status` action/spool round-trip: PASS.

## Task 5 — read-only EPLAN inspection — PASS

Fixed operation surface:

- `project.getActive`;
- `project.listPages`;
- `selection.get`;
- `object.get` (Function-only).

`ProjectSessionTracker` binds compact object references to one live EPLAN database/session. Stale references fail closed. No arbitrary property IDs or whole DataModel objects are exposed.

Real project evidence: `УСПД Жуковский ПНС`, database ID `25`, 7 pages.

## Task 6 — LegacyHost RemoteClient gateway — PASS

`EplanRemoteClientAdapter` is the only class that references EPLAN RemoteClient. `RemoteGateway` owns connection/session policy. The only outgoing EPLAN action is:

```text
AudeuAiDispatch /REQUESTID:<canonical-guid>
```

Real workstation acceptance proved x64 EPLAN server discovery, connect, `SynchronousMode = true`, ping and a persistent same-session read sequence through `object.get`.

## Task 7 — secured named pipe — PASS

Fixed pipe:

```text
Audeu.EplanAi.LegacyHost.v1
```

Properties:
- current-user-only Windows pipe ACL;
- fixed local pipe name; caller cannot supply pipe/host/path;
- canonical camelCase `BridgeRequest` / `BridgeResponse` JSON;
- 4-byte little-endian length prefix;
- request max 1 MiB;
- serialized EPLAN execution;
- persistent RemoteGateway lifetime;
- graceful shutdown.

Real workstation acceptance 2026-10-26:

```text
PASS: system.status through named pipe (status=ready, protocol=1.0)
PASS: project.getActive through named pipe
  projectSessionId : 21b58d12-1213-4876-aff1-fb62a4dcad5d
  databaseId       : 25
  name             : УСПД Жуковский ПНС
  pageCount        : 7
PASS: project.listPages through named pipe with same projectSessionId (items=7)
PASS: selection.get through named pipe with same projectSessionId (items=1)
PASS: object.get through named pipe for selected Function: ===+++УСПД_Э3#-A1
PASS: Task 7 named-pipe persistent read sequence completed successfully.
PASS: clean Ctrl+C / RemoteClient shutdown.
```

Task 7 request-ID camelCase regression was reproduced with RED coverage and fixed before workstation acceptance.

## Task 8 — MCP read-only tools — PASS / MERGED

Canonical issue: #20 (completed). Branch: `m0/task-8-mcp-readonly`. PR: #21 (merged to `main`).

### MCP transport

`Audeu.EplanAi.Mcp` targets `net8.0` and references only shared Contracts plus MCP/hosting packages; it has no EPLAN assembly reference.

Production transport:
- fixed server `.` and pipe `Audeu.EplanAi.LegacyHost.v1`;
- one connection per tool call;
- production connect timeout 10 seconds;
- Task 7-compatible canonical camelCase framing;
- request max 1 MiB / response max 16 MiB;
- request-ID correlation and protocol-version validation;
- caller cancellation propagation;
- deterministic transport error mapping.

### Exposed MCP tools

Exactly five closed-world read-only tools:

- `eplan_status` -> `system.status`;
- `eplan_get_active_project` -> `project.getActive`;
- `eplan_list_pages(projectSessionId)` -> `project.listPages`;
- `eplan_get_selection(projectSessionId)` -> `selection.get`;
- `eplan_get_object(projectSessionId, databaseIdentifier, typeIdentifier, objectIdentifier, stringIdentifier)` -> `object.get`.

Every tool is annotated `ReadOnly=true`, `Destructive=false`, `Idempotent=true`, `OpenWorld=false`. No pipe name, host, EPLAN action, filesystem path, property ID or arbitrary bridge operation can come from model input.

Successful bridge `DataJson` is returned unchanged as compact JSON text. Stable bridge failures become controlled MCP tool errors. Unknown error codes fail closed as `INTERNAL_ERROR`.

### Task 8 TDD / CI evidence

- pipe codec RED: CI #245; only missing MCP transport namespace after test-import correction;
- named-pipe client RED: CI #248; only missing `NamedPipeBridgeClient`;
- tool mapping RED: CI #251; only missing MCP Tools namespace;
- host composition RED: CI #253; only missing `Program.CreateBuilder`;
- host dependency issue exposed by CI #254 and fixed with pinned `Microsoft.Extensions.Hosting` 8.0.0;
- test-runner audit rejected zero-discovery CI #256 as invalid test evidence;
- corrected xUnit v3 runner CI #257: Contracts **18/18**, LegacyHost **45/45**, AddIn **31/31**, MCP **17/17**, MCP build PASS, both prerequisite guards PASS;
- final Task 8 branch CI #261 PASS;
- post-merge `main` CI #303 PASS.

Task 8 real MCP-to-EPLAN runtime acceptance is proven by Task 9 / Gate B.

## Task 9 — Gate B real MCP-to-EPLAN acceptance — PASS / MERGED

Canonical issue: #22 (completed). Branch: `m0/task-9-gate-b`. PR: #23 (merged to `main`).

The workstation probe uses the official `ModelContextProtocol` 2.3.0 `StdioClientTransport` and `McpClient`; it does not load or reference EPLAN assemblies.

Final real workstation evidence on 2026-10-27:

```text
PASS: MCP discovery exposes exactly five approved read-only tools
PASS: eplan_status through real MCP stdio client (status=ready, protocol=1.0)
PASS: eplan_get_active_project (name=УСПД Жуковский ПНС, pages=7, valid session)
PASS: eplan_list_pages through MCP (items=7)
PASS: eplan_get_selection through MCP (Function: ===+++УСПД_Э3#-A1)
PASS: eplan_get_object through MCP; Function Text exactly matches 'Контроллер УСПД БКД-ПК-RF.2'
PASS: closed-project MCP read returned NO_ACTIVE_PROJECT and cleared the old project session
PASS: reopened project established a fresh session
PASS: old Function reference rejected through MCP with SESSION_STALE
PASS: Gate B real MCP-to-EPLAN read-only acceptance completed successfully
PASS: Task 9 Gate B probe exited cleanly
PASS: LegacyHost clean Ctrl+C shutdown
```

### Real-acceptance defects found and fixed

1. **Windows PowerShell 5.1 Cyrillic script decoding.** A UTF-8-without-BOM literal project name was decoded as mojibake. Workstation scripts are now ASCII-safe where required and an encoding guard rejects unsafe `.ps1` files.
2. **EPLAN multilingual Function Text representation.** `FUNC_TEXT.ToString()` exposed internal `??_??@...;` serialization. `EplanReadAdapter` now uses `PropertyValue.ToMultiLangString()` and `Translate.GetStringToDisplayInDialogs(project, ...)` so the bridge exposes the displayed EPLAN text. Regression coverage uses the exact real value `??_??@Контроллер УСПД БКД-ПК-RF.2;`.
3. **Official MCP SDK tool-error wrapper.** The SDK wraps stable bridge errors with text such as `An error occurred invoking 'eplan_list_pages': NO_ACTIVE_PROJECT: ...`. Probe matching now validates the stable code without depending on the wrapper prefix. The dedicated regression suite covers raw/wrapped `NO_ACTIVE_PROJECT`, wrapped `SESSION_STALE` and false-positive rejection.

Final merged verification:

- Contracts **18/18 PASS**;
- LegacyHost **45/45 PASS**;
- AddIn **32/32 PASS**;
- MCP **17/17 PASS**;
- MCP probe regressions **6/6 PASS**;
- Task 9 probe build PASS;
- Windows PowerShell 5.1 validation PASS;
- AddIn/LegacyHost prerequisite guards PASS;
- `main` CI #304 PASS;
- PowerShell Encoding Guard #31 PASS.

## Task 10 — immutable MCP Change Plan lifecycle — REPO-SIDE PASS

Canonical issue: #24. Branch: `m1/task-10-change-plans`. PR: #25 (ready for review; unmerged pending explicit approval).

Task 10 introduces no EPLAN write path and no new MCP tool. Its purpose is to make a future write single-use, session-bound and based on an immutable preview.

Implemented:

- `ChangeOperation` defensively snapshots all five fields of `ObjectReference`; callers cannot mutate a stored plan by changing either the source reference or a value returned from `Target`;
- stable `PLAN_NOT_FOUND` and `PLAN_STALE` protocol errors;
- singleton in-memory `ChangePlanStore` with synchronized lookup/session validation;
- atomic `ClaimForApply`: concurrent callers attempting to use one plan produce exactly one success and all later attempts fail with `PLAN_ALREADY_APPLIED`;
- terminal `previewed -> applied|failed` lifecycle;
- `FunctionTextChangePlanner` snapshots the target before its first await, validates a maximum of 1024 UTF-16 code units, performs exactly one existing `object.get` request with `Mode = read`, captures displayed old Function Text and stores one immutable `setFunctionText` plan;
- malformed/non-Function/unknown responses fail closed; known stable bridge errors are preserved;
- `TimeProvider.System`, store and planner are singleton DI services in the MCP host;
- external MCP registration remains `.WithTools<EplanReadTools>()`, so discovery still exposes exactly the five read-only M0 tools.

Task 10 TDD evidence:

- immutable-contract RED: CI #306;
- contract GREEN: CI #308, Contracts **20/20 PASS**;
- store RED: CI #309; missing ChangePlans types only;
- store GREEN: CI #311;
- planner RED: CI #312; missing `FunctionTextChangePlanner` only;
- planner GREEN: CI #314;
- DI-composition RED: CI #315; 37/38 MCP tests passed and the newly added unregistered-service test failed;
- final implementation GREEN: CI #317 — Contracts **20/20**, LegacyHost **45/45**, AddIn **32/32**, MCP **38/38**, MCP probe **6/6**, MCP/probe builds and prerequisite guards PASS;
- final documentation head `4abf2e074b1b7e991eba2db902e83c250f821314`: CI #318 SUCCESS and PowerShell Encoding Guard #45 SUCCESS.

Safety boundary at the end of Task 10: there was still no EPLAN write path and no mutating MCP tool. Task 11 adds only the approved internal AddIn write boundary; the MCP surface remains read-only until Task 12.

## Task 11 — internal safe Function Text write path — REPO-SIDE PASS

Canonical issue: #26. Branch: `m1/task-11-safe-function-text`. PR: #27 (stacked on Task 10; unmerged pending explicit approval).

Task 11 implements the internal bridge mutation boundary for the single approved v0.1 write: applying and reversing a previewed `setFunctionText` operation. It does **not** expose a model-callable write MCP tool and it performs **no real workstation mutation** during repo-side completion.

Fixed internal bridge operations:

- `change.apply`;
- `change.undoLast`.

Safety and state guarantees:

- read and write services share one long-lived `ProjectSessionTracker` in the default AddIn registry;
- write requests require `Mode = write` and a current project session;
- project or Function read-only state returns stable `READ_ONLY_PROJECT` before mutation;
- `change.apply` accepts exactly the fixed Change Plan payload; no caller-controlled property name/ID, EPLAN action or arbitrary write operation is accepted;
- `changePlanId` is atomically claimed before adapter mutation and remains single-use even after a safe failure, so ambiguous retries require a fresh preview;
- target resolution rechecks active project, database/type/object/string identity, validity and Function type;
- displayed `FUNC_TEXT` is re-read through the same `Translate.GetStringToDisplayInDialogs(...)` boundary as read operations and must exactly equal the previewed old value; mismatch returns `PLAN_STALE` without mutation;
- the only mutated property is Function `FUNC_TEXT`, built through `MultiLangString.SetAsString(newValue)`;
- persistent changes are bounded by `UndoStep`, `SafetyPoint` and `Transaction`;
- displayed text is read back and must exactly equal the requested new value before commits;
- `Transaction.IsImplicitEplanTransactionCommited` is checked and any uncertain atomicity fails closed;
- success commits Transaction before SafetyPoint;
- exception or verification failure before commit leaves SafetyPoint uncommitted so stubbed rollback tests restore the original value;
- bridge-level `change.undoLast` stores one session-bound last-change snapshot and performs a guarded reverse write; it does not keep a historical disposable `UndoStep` alive across calls;
- arbitrary operations such as `eplan.setProperty` remain rejected with `BRIDGE_UNKNOWN_OPERATION`;
- MCP host composition is unchanged and still registers only `.WithTools<EplanReadTools>()`, preserving exactly five model-callable read-only tools.

Task 11 TDD evidence:

- write-service RED: CI #320; Contracts **20/20** and LegacyHost **45/45** passed, AddIn failed only because the new WriteModel types did not yet exist;
- service-policy GREEN: CI #325 SUCCESS;
- fixed-registry/shared-session RED: CI #326; failed only because the two-service `OperationRegistry` constructor was not yet implemented;
- registry/shared-session GREEN: CI #327 SUCCESS;
- transactional-adapter RED: CI #329; failed only because `EplanWriteAdapter` / `IWriteExecutionHook` were not yet implemented;
- adapter GREEN: CI #330 AddIn step PASS after implementation of target validation, old-value guard, UndoStep/SafetyPoint/Transaction, language-independent write, read-back verification and rollback test seam;
- default-dispatch RED: CI #331 — AddIn **58/60 PASS**, with exactly the two new fixed-write dispatch tests failing because `change.apply` / `change.undoLast` still returned `BRIDGE_UNKNOWN_OPERATION`;
- final code GREEN at `4280b0af55ad22d6db546a23f6154bbdad742ef4`: CI #332 SUCCESS — Contracts **20/20**, LegacyHost **45/45**, AddIn **60/60**, MCP **38/38**, MCP probe **6/6**, MCP/probe builds, PowerShell 5.1 validation and both EPLAN prerequisite guards PASS;
- PowerShell Encoding Guard #59 SUCCESS on the same code head.

Task 11 boundary: repository tests prove the closed internal write/rollback path with EPLAN-shaped API stubs only. Real EPLAN 2.9.4 mutation, verification and rollback evidence deliberately remains Task 12 / Gate C work and must use a disposable/safe project.

## Immediate next actions

1. Finalize PR #27 as ready for review after the documentation-only final head passes CI; do not merge PR #27 or its Task 10 base PR #25 without explicit user approval.
2. Begin Task 12 as a separate stacked increment: expose the approved MCP preview/apply/undo tools while preserving closed-world arguments and Change Plan single-use semantics.
3. Execute Gate C only on a disposable/safe EPLAN project: preview -> prove unchanged -> apply -> verify -> undo -> verify original -> injected failure -> verify rollback.
4. Keep all production/project work untouched until Gate C has passed; no mutation testing on a production EPLAN project.
5. Preserve the closed-world rule: no arbitrary EPLAN property IDs, action names, paths, code or unrestricted DataModel access may become model-controlled.

## Release 0.1 boundaries

Included:
- status/connection;
- active project;
- page list;
- current selection;
- Function inspection;
- one Function Text Preview -> Apply -> Verify -> Undo/Rollback flow after Gate B.

Deferred:
- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- arbitrary EPLAN property/action access.

## Working rules

1. GitHub is project memory/source of truth.
2. Update this file after material implementation/evidence/blockers.
3. Prefer small stacked branches/PRs.
4. Never test mutating operations on a production EPLAN project.
5. No arbitrary code, property ID, EPLAN action name or filesystem path may be model-controlled.
6. Durable object identity is never inferred from session-scoped EPLAN IDs.
7. Require non-zero executed test counts for CI evidence.
8. Do not merge project PRs without explicit user approval.