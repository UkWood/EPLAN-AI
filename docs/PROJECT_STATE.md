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
- **Gate C / M1 safe write:** NOT STARTED. Gate B no longer blocks Task 10 planning, but no mutating behavior has been implemented yet.

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
- [ ] Task 10: immutable MCP Change Plan state machine for `setFunctionText`.
- [ ] Task 11: safe Function Text preview/apply in EPLAN.
- [ ] Task 12 / Gate C: MCP apply/undo and M1 acceptance.
- [ ] Task 13 / Gate D: final verification and v0.1 packaging.

Tasks 1–7 were integrated to `main` at merge commit `1064c841d0b33f29b649b03c7dd902fd3f4a7bbd`; main CI #239 (`40360507085`) passed after integration.

Tasks 8–9 completed M0 integration on 2026-10-27:

- PR #21 -> `main` merge commit `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`; main CI #303 PASS; Issue #20 completed.
- PR #23 -> `main` merge commit `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`; main CI #304 PASS; PowerShell Encoding Guard #31 PASS; Issue #22 completed.

## Test runner ruling

xUnit v3 test projects are executable Microsoft Testing Platform applications. A CI step is accepted as test evidence only when it reports a non-zero executed-test count.

Task 8 initially exposed a zero-discovery MCP test configuration. That green-looking run was rejected as invalid evidence. `Audeu.EplanAi.Mcp.Tests` was corrected with `OutputType=Exe` and `TestingPlatformDotnetTestSupport=true`; CI #257 then executed and passed **17/17 MCP tests**.

Task 9 later added a dedicated MCP probe regression suite. Final Task 9 runs execute and pass **6/6 MCP probe tests** in addition to the existing suites. Final merged `main` CI #304 passed the full repository pipeline.

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

## Immediate next actions

1. Begin Task 10 design/plan for the immutable in-memory Change Plan state machine supporting only `setFunctionText`.
2. Preserve the mandatory Preview -> Apply separation: preview may read EPLAN but must not write.
3. Bind each plan to the current `projectSessionId`, exact Function reference, old value and proposed new value; plans are immutable and single-use.
4. Keep all future write validation on a disposable/safe EPLAN test project until Gate C passes.
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
