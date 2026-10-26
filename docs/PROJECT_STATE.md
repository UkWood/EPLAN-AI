# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-26

## Purpose

Living restart point for the project. Update after every meaningful implementation step, architectural decision, blocker, test result, or milestone change.

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
        | EPLAN Remoting
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

The LLM never receives unrestricted EPLAN API access. All operations pass through a small versioned bridge/domain API with fixed operation names, validation and later immutable Change Plans.

## Approved documents

- `docs/design/eplan-ai-bridge-v0.1.md`
- `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- `docs/setup/eplan-2.9-prerequisites.md`
- `docs/setup/task4-local-validation.md`
- `docs/superpowers/specs/2026-10-26-task-5-readonly-eplan-design.md`
- `docs/superpowers/plans/2026-10-26-task-5-readonly-eplan.md`
- `docs/setup/task5-local-validation.md`
- `docs/superpowers/specs/2026-10-26-task-6-remoteclient-design.md`
- `docs/superpowers/plans/2026-10-26-task-6-remoteclient.md`
- `docs/setup/task6-local-validation.md`

## Verified target workstation

Confirmed 2026-10-23 and extended by real runtime/build evidence 2026-10-26:

- EPLAN Electric P8 Professional 64-bit;
- 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting DLLs present;
- x64 .NET SDK available under `C:\Program Files\dotnet`;
- AddIn compiles against the real 2.9.4 assemblies;
- EPLAN loads the AddIn through `%APPDATA%\EPLAN\ShadowCopyAssemblies`;
- `AudeuAiDispatch` registers as ordinal `20` and executes;
- real `system.status` spool round-trip PASS;
- real Task 5 `project.getActive` PASS for project `УСПД Жуковский ПНС`, database ID `25`, 7 pages, read-only `False`;
- Task 6 production `Audeu.EplanAi.LegacyHost` compiles successfully against the workstation's actual EPLAN 2.9.4 `RemoteClientu.dll` / `Remotingu.dll`.

Gate status:

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime:** PASS.
- **Gate A2 — Task 5 DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly compatibility:** PASS.
- **Gate A2 — RemoteClient runtime discovery/connect/action execution:** pending Task 6 runtime probe.
- **Gate B / M0:** pending Task 6 real RemoteClient runtime acceptance, Task 7 named pipe, Task 8 MCP read tools and final Task 9 acceptance.

## Current status

- [x] Task 1: repository scaffold / CI — draft PR #3.
- [x] Task 2: versioned contracts — Issue #4 completed, draft PR #5.
- [x] Task 3: secure local spool — Issue #6 completed, draft PR #7.
- [x] Task 4: AddIn + fixed `AudeuAiDispatch` — Issue #8 completed, draft PR #11.
- [x] Task 5: project session + read-only EPLAN inspection — Issue #12 completed, draft PR #15.
- [~] Task 6: LegacyHost RemoteClient gateway — repo-side implementation/CI PASS and real EPLAN 2.9.4 production build PASS; real runtime validation pending. Issue #16, draft PR #17, branch `m0/task-6-remoteclient`.
- [ ] Task 7: current-user named-pipe LegacyHost.
- [ ] Task 8: MCP read-only tools.
- [ ] Task 9 / Gate B: complete read-only bridge acceptance.
- [ ] M1 / Gate C: Function Text Preview -> Apply -> Verify -> Undo/Rollback.

All existing PRs remain unmerged. Do not merge without explicit user approval.

## Test runner ruling

xUnit v3 test projects are executable test applications. A CI step is only accepted as evidence when it reports a non-zero executed-test count.

## Task 1 — scaffold

Solution/project shells, shared MSBuild configuration, EPLAN API path guards, MCP/Contracts shells and Windows CI.

Evidence: CI `39959463954` success.

## Task 2 — contracts

Protocol `1.0`, `BridgeRequest` / `BridgeResponse`, session-scoped `ObjectReference`, stable errors and initial Change Plan contracts.

Evidence: CI `39975775380`, Contracts **18/18**.

## Task 3 — secure spool

Fixed root `%LOCALAPPDATA%\Audeu\EplanAiBridge`, GUID file names, request/response size limits, same-directory temp+rename, duplicate fail-closed behavior, response-ID verification and stale cleanup.

Evidence: final CI `39976839719`, Contracts **18/18**, LegacyHost spool **8/8**.

## Task 4 — fixed AddIn dispatch — PASS

Implemented `EplanAddIn : IEplAddIn`, fixed `AudeuAiDispatchAction : IEplAction`, secure `REQUESTID`, `system.status`, diagnostics and UTF-8/BOM handling.

Important real-API findings:

- installed EPLAN 2.9.4 `ActionProperties` has no `Description` member;
- `OnRegister()` need not repeat for an already registered AddIn;
- Windows PowerShell 5.1 `-Encoding UTF8` writes BOM, so spool reading is BOM-tolerant.

Final CI `40303614597`: Contracts **18/18**, spool **8/8**, AddIn **13/13**, MCP/guards PASS.

Real EPLAN `system.status` round-trip PASS with `Execute:entered` -> matching request ID -> `Execute:success`.

## Task 5 — read-only EPLAN inspection — PASS

Fixed operation surface:

- `project.getActive`;
- `project.listPages`;
- `selection.get`;
- `object.get` (Function-only).

`ProjectSessionTracker` binds references to one live EPLAN database/session. Stale references fail closed. No arbitrary property IDs or write APIs are exposed.

Final repo CI `40309525419`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **31/31**, MCP/guards PASS.

Real Task 5 build against installed EPLAN 2.9.4: PASS.

Real `project.getActive`:

```text
PASS: Task 5 project.getActive succeeded for request 181d5d4a-9818-49e0-843a-327c27a642c4
projectSessionId : e4caa3c7-e888-47be-ae0c-42b51403df9f
databaseId       : 25
name             : УСПД Жуковский ПНС
fullName         : E:\EPLAN\УСПД Жуковский ПНС
pageCount        : 7
isReadOnly       : False
```

## Task 6 — LegacyHost RemoteClient gateway — repo-side PASS / real build PASS / runtime pending

Canonical tracking: Issue #16. Draft PR #17. Branch `m0/task-6-remoteclient`, stacked on validated Task 5.

### Design

LegacyHost remains engineering-logic-free. A thin `EplanRemoteClientAdapter` is the only class that references `Eplan.EplApi.RemoteClient`. `RemoteGateway` owns connection/session policy and the existing secure spool round-trip.

The only EPLAN action it can construct is:

```text
AudeuAiDispatch /REQUESTID:<canonical-guid>
```

No caller can provide an EPLAN action name, host, port or filesystem path.

### Implemented RemoteClient behavior

- discover active local EPLAN servers;
- accept only 64-bit `Electric P8` version `2.9*` candidates;
- deterministic selection by server port, then process ID;
- `Connect("localhost", port, timeout)`;
- force `SynchronousMode = true`;
- require successful `Ping()`;
- fixed-action spool round-trip;
- stable `EPLAN_NOT_FOUND`, `EPLAN_NOT_CONNECTED`, `EPLAN_ACTION_FAILED`, `OPERATION_TIMEOUT` errors;
- action timeout poisons/disconnects the current gateway connection;
- persistent connection allows Task 5 requests to reuse one `projectSessionId`.

Official EPLAN 2.9 API signatures used by the adapter are isolated behind `IRemoteClientAdapter` and test-only verified stubs. The installed workstation DLL is authoritative for final compatibility.

### Task 6 TDD evidence

Remote contracts:

- RED commit `05d3de33c83d30027ad84432a311c48625855a18`, CI `40312106175`: Contracts **18/18**, then LegacyHost compile failed because Remote types were absent;
- minimal contract implementation followed;
- one test-only reflection ordering assumption was corrected;
- GREEN CI `40312656987`.

Gateway policy:

- RED commit `117b991d6c05e6d0b446a50c4f78fdf5aa899fac`, CI `40313041462`: compile failed only because `RemoteGateway` was absent;
- gateway implementation commit `dd07bd6c897afd7c5dc766acdad6786ef13686ce`;
- full CI GREEN.

Real EPLAN adapter:

- test-only RemoteClient API stubs and adapter contract added first;
- RED commit `2303cb09042be9771451fbfeceb86f810b5bf2f2`, CI `40313685998`: compile failed only because `EplanRemoteClientAdapter` was absent;
- production adapter commit `ae4350498833c7467d5d09652584404883dd6021`;
- first subsequent run compiled and executed **20/21** LegacyHost tests, exposing one transient test failure without diagnostics;
- CI was hardened to print LegacyHost test logs;
- unchanged production implementation then passed full CI and an explicit rerun;
- final repo-side implementation run `40315002086`: Contracts **18/18**, LegacyHost **21/21**, AddIn **31/31**, MCP build and both prerequisite guards PASS;
- fresh documentation head CI `40315359634` also PASS.

### Task 6 real-workstation build — PASS

Command:

```powershell
.\scripts\verify-task6-build.ps1
```

Observed on the verified workstation:

```text
Building Task 6 LegacyHost against real EPLAN RemoteClient API: C:\Program Files\EPLAN\Platform\2.9.4\Bin
Audeu.EplanAi.Contracts netstandard2.0 successfully built
Audeu.EplanAi.LegacyHost net472 win-x86 successfully built
Build succeeded
PASS: Task 6 LegacyHost compiled against real EPLAN 2.9.4 RemoteClient assemblies: E:\EPLAN\REPO\EPLAN-AI\src\Audeu.EplanAi.LegacyHost\bin\Release\net472\Audeu.EplanAi.LegacyHost.exe
```

This proves production LegacyHost compatibility with the workstation's actual EPLAN 2.9.4 RemoteClient assemblies. Remaining Task 6 risk is runtime discovery/connect/remoting behavior rather than compile-time API mismatch.

### Task 6 runtime probe — pending

1. Start EPLAN normally, without `/Auto` and without `/NoRemoting`.
2. Open the safe test project and leave EPLAN running and idle.
3. Prefer selecting one Function so optional `object.get` can be exercised.
4. In a second PowerShell window run:

```powershell
.\scripts\test-task6-remoteclient.ps1
```

The diagnostic LegacyHost mode keeps one Remoting connection alive and attempts:

```text
connect + ping
system.status
project.getActive
project.listPages (same projectSessionId)
selection.get (same projectSessionId)
object.get (if a Function is selected)
disconnect
```

Task 6 remains open until this persistent RemoteClient sequence passes on real EPLAN 2.9.4.

## Immediate next actions

1. Do not merge PR #3, #5, #7, #11, #15 or #17 without explicit user approval.
2. Keep EPLAN running normally with the safe project open; preferably select one Function.
3. Run `scripts\test-task6-remoteclient.ps1` from a second PowerShell and preserve the complete output.
4. If runtime discovery/connect fails, debug the actual EPLAN 2.9 Remoting state; real assembly compatibility is already proven.
5. Only after Task 6 real runtime acceptance start Task 7 named-pipe work.
6. Do not start M1 write work until Gate B / M0 passes.

## Release 0.1 boundaries

Included:
- status/connection;
- active project;
- page list;
- current selection;
- Function inspection;
- one Function Text Preview -> Apply -> Verify -> Undo/Rollback flow.

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
