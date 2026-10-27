# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-26

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

## Verified target workstation

Confirmed through real build/runtime evidence on 2026-10-26:

- EPLAN Electric P8 Professional 64-bit;
- version 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting DLLs present;
- x64 .NET SDK 8.0.427 available under `C:\Program Files\dotnet`;
- workstation PATH also contains `C:\Program Files (x86)\dotnet` before x64, so Task 7 testing temporarily prepended `C:\Program Files\dotnet` to the PowerShell process PATH;
- AddIn compiles against the real EPLAN 2.9.4 assemblies and loads in EPLAN;
- fixed action `AudeuAiDispatch` registers and executes;
- real RemoteClient discovery/connect/ping succeeds;
- real persistent read sequence succeeds against project `УСПД Жуковский ПНС`, database ID `25`, 7 pages.

## Gate status

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime / real DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly/runtime compatibility:** PASS.
- **Tasks 1–7 integration:** PASS and merged to `main`.
- **Task 8 repo-side MCP implementation:** PASS; real MCP-to-EPLAN runtime is intentionally deferred to Task 9.
- **Gate B / M0 read-only bridge:** PENDING Task 9 workstation acceptance.
- **Gate C / M1 safe write:** NOT STARTED; write work remains prohibited until Gate B passes.

## Current status

- [x] Task 1: repository scaffold / CI.
- [x] Task 2: versioned contracts.
- [x] Task 3: secure local spool.
- [x] Task 4: AddIn + fixed `AudeuAiDispatch`.
- [x] Task 5: project session + read-only EPLAN inspection.
- [x] Task 6: LegacyHost RemoteClient gateway.
- [x] Task 7: secured current-user named-pipe LegacyHost.
- [x] Task 8: .NET 8 stdio MCP read-only tools — repo-side implementation complete on `m0/task-8-mcp-readonly`; PR/final branch CI follows.
- [ ] Task 9 / Gate B: complete real MCP-to-EPLAN read-only acceptance, including stale-session behavior.
- [ ] M1 / Gate C: Function Text Preview -> Apply -> Verify -> Undo/Rollback.

Tasks 1–7 were integrated to `main` at merge commit `1064c841d0b33f29b649b03c7dd902fd3f4a7bbd`; main CI #239 (`40360507085`) passed after integration.

## Test runner ruling

xUnit v3 test projects are executable Microsoft Testing Platform applications. A CI step is accepted as test evidence only when it reports a non-zero executed-test count.

Task 8 initially exposed a zero-discovery MCP test configuration. That green-looking run was rejected as invalid evidence. `Audeu.EplanAi.Mcp.Tests` was corrected with `OutputType=Exe` and `TestingPlatformDotnetTestSupport=true`; CI #257 then executed and passed **17/17 MCP tests**.

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

## Task 8 — MCP read-only tools — REPO-SIDE PASS

Canonical issue: #20. Branch: `m0/task-8-mcp-readonly`.

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

Successful bridge `DataJson` is returned unchanged as compact JSON text. Stable bridge failures become controlled `McpException` errors beginning with the stable error code. Unknown error codes fail closed as `INTERNAL_ERROR`.

### Task 8 TDD / CI evidence

- pipe codec RED: CI #245; only missing MCP transport namespace after test-import correction;
- pipe codec implementation: head `9013e1413ae3b67366bb0bf96e43c39864023555`;
- named-pipe client RED: CI #248; only missing `NamedPipeBridgeClient`;
- named-pipe client implementation: `95632a0614948899b0cec6ceb3a72127a36caf74`;
- tool mapping RED: CI #251; only missing MCP Tools namespace;
- tool implementation: `fe8a6473a9d4e41b8171137d44a86ee06535c1d3`;
- host composition RED: CI #253; only missing `Program.CreateBuilder`;
- host implementation initially exposed missing `Microsoft.Extensions.Hosting` reference in CI #254; fixed with pinned `Microsoft.Extensions.Hosting 8.0.0`;
- test-runner audit rejected zero-discovery CI #256 as invalid test evidence;
- corrected xUnit v3 runner CI #257: Contracts **18/18**, LegacyHost **45/45**, AddIn **31/31**, MCP **17/17**, MCP build PASS, both prerequisite guards PASS.

Real MCP-to-EPLAN stdio acceptance is deliberately **not** claimed here; it is Task 9 / Gate B.

## Immediate next actions

1. Finish Task 8 final branch/PR CI and keep the PR scoped to read-only MCP plumbing.
2. Prepare Task 9 as a small stacked acceptance increment with repeatable workstation instructions in `docs/setup/local-integration-test.md`.
3. On the disposable EPLAN test project verify through the actual MCP interface:
   - EPLAN detection;
   - active project;
   - page list/count;
   - selected Function;
   - expected Function Text inspection;
   - close/reopen invalidates the old object reference with `SESSION_STALE`.
4. Mark Gate B PASS only after all six checks pass.
5. Do not start Task 10 or any write behavior before Gate B PASS.

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
