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

Security boundary:

- the LLM never receives unrestricted EPLAN API access;
- MCP exposes only a closed-world domain API;
- MCP references no EPLAN assembly;
- LegacyHost is the only process that knows EPLAN RemoteClient;
- AddIn is the only component that accesses EPLAN DataModel/HEServices;
- no arbitrary property IDs, EPLAN action names, bridge operations, filesystem paths or code are model-controlled;
- mutating work is previewed, represented as an immutable Change Plan, applied by plan ID only, verified and reversible.

## Verified workstation

Confirmed through real build/runtime evidence on 2026-10-26 and 2026-10-27:

- EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting DLLs present;
- x64 .NET SDK available under `C:\Program Files\dotnet`;
- AddIn compiles against the real EPLAN 2.9.4 assemblies and loads in EPLAN;
- fixed action `AudeuAiDispatch` registers and executes;
- real RemoteClient discovery/connect/ping succeeds;
- official MCP stdio client reaches EPLAN end-to-end through all bridge layers;
- real read-only and safe Function Text write/undo/rollback flows have passed workstation acceptance.

## Gate status

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime / real DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly/runtime compatibility:** PASS.
- **Gate B / M0 read-only bridge:** PASS and integrated to `main`.
- **Gate C / M1 safe write:** **PASS and integrated to `main`**.
- **Gate D / Task 13 final verification and v0.1 packaging:** NOT STARTED.

## Current status

- [x] Task 1: repository scaffold / CI.
- [x] Task 2: versioned contracts.
- [x] Task 3: secure local spool.
- [x] Task 4: AddIn + fixed `AudeuAiDispatch`.
- [x] Task 5: project session + read-only EPLAN inspection.
- [x] Task 6: LegacyHost RemoteClient gateway.
- [x] Task 7: secured current-user named-pipe LegacyHost.
- [x] Task 8: .NET 8 stdio MCP read-only tools.
- [x] Task 9 / Gate B: real MCP-to-EPLAN read-only acceptance including stale-session behavior.
- [x] Task 10: immutable MCP Change Plan lifecycle for `setFunctionText`.
- [x] Task 11: internal safe Function Text apply/undo path inside the EPLAN boundary.
- [x] Task 12 / Gate C: model-callable preview/apply/undo surface plus real safe-write acceptance.
- [ ] Task 13 / Gate D: final verification, release documentation and v0.1 packaging.

## Integration history

### M0

Tasks 1–7 were integrated to `main` at merge commit:

```text
1064c841d0b33f29b649b03c7dd902fd3f4a7bbd
```

Task 8:

- PR #21 merged to `main` at `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`;
- main CI #303 PASS;
- Issue #20 completed.

Task 9 / Gate B:

- PR #23 merged to `main` at `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`;
- main CI #304 PASS;
- PowerShell Encoding Guard #31 PASS;
- Issue #22 completed.

### M1

Task 10:

- PR #25 merged to `main` at `3f81afa39ab51efe1ec48f48c20035ab2dd4d594`;
- main CI #359 PASS;
- PowerShell Encoding Guard #86 PASS;
- Issue #24 completed.

Task 11:

- PR #27 retargeted to `main` after Task 10 integration;
- PR #27 merged to `main` at `5243179757989d4bf10e19ff53e71fb07a34ecfb`;
- main CI #360 PASS;
- Issue #26 completed.

Task 12 / Gate C:

- PR #29 retargeted to `main` after Task 11 integration;
- PR #29 merged to `main` at `e3af813b7d238f5d44eb3f41ea438c032c0ae3d9`;
- main CI #361 PASS;
- PowerShell Encoding Guard #88 PASS;
- Issue #28 completed.

M1 is therefore fully integrated into `main`.

## Current MCP surface

Exactly eight approved model-callable tools are exposed.

Read-only:

- `eplan_status`;
- `eplan_get_active_project`;
- `eplan_list_pages`;
- `eplan_get_selection`;
- `eplan_get_object`.

Safe write workflow:

- `eplan_preview_set_function_text`;
- `eplan_apply_change_plan`;
- `eplan_undo_last_ai_change`.

Closed-world rules:

- preview accepts one compact Function reference plus proposed Function Text;
- apply accepts only `changePlanId`;
- undo accepts only the current `projectSessionId`;
- target, old value and new value cannot be resubmitted or altered during apply;
- no generic set-property tool exists;
- no arbitrary EPLAN action/property/bridge operation/path/code is model-controlled.

## Task 10 — immutable Change Plan lifecycle

Implemented:

- defensive target snapshots;
- stable `PLAN_NOT_FOUND`, `PLAN_STALE`, `PLAN_ALREADY_APPLIED` behavior;
- synchronized in-memory Change Plan store;
- atomic single-use claim for apply;
- terminal `previewed -> applied|failed` lifecycle;
- 1024 UTF-16 Function Text limit;
- preview performs read-only `object.get` and stores exact displayed old/new values.

Task 10 repo-side verification reached Contracts **20/20**, LegacyHost **45/45**, AddIn **32/32**, MCP **38/38**, MCP probe **6/6** before integration.

## Task 11 — internal safe Function Text write boundary

Fixed internal bridge operations:

- `change.apply`;
- `change.undoLast`.

Safety properties:

- read/write services share one `ProjectSessionTracker`;
- exact current session and Function identity are revalidated before mutation;
- read-only state fails before mutation;
- current displayed Function Text must equal the previewed old value or apply fails stale;
- only `FUNC_TEXT` is written;
- persistent mutation is bounded by `UndoStep`, `SafetyPoint` and API `Transaction`;
- displayed text is read back and verified before commit;
- uncertain implicit EPLAN transaction behavior fails closed;
- pre-commit failure rolls back;
- undo uses a guarded current-session last-change snapshot.

Task 11 repo-side verification reached AddIn **60/60** with all existing suites green before integration.

## Task 12 / Gate C — safe MCP write flow

Exactly three write-workflow tools were added without opening a generic write surface.

Final Task 12 repo-side verification before workstation acceptance:

- Contracts **20/20 PASS**;
- LegacyHost **45/45 PASS**;
- AddIn **64/64 PASS**;
- MCP **54/54 PASS**;
- MCP probe **17/17 PASS**;
- CI #354 PASS;
- PowerShell Encoding Guard #81 PASS.

The final Task 12 branch head after acceptance documentation also passed CI #358 and PowerShell Encoding Guard #85 before merge.

## Gate B real workstation evidence — PASS

Project: `УСПД Жуковский ПНС`.

Selected Function:

```text
===+++УСПД_Э3#-A1
```

Displayed Function Text:

```text
Контроллер УСПД БКД-ПК-RF.2
```

Accepted behavior included:

- exact five-tool M0 discovery;
- status / active project / pages / selection / Function inspection;
- exact displayed multilingual Function Text;
- `NO_ACTIVE_PROJECT` after project close;
- fresh project session after reopen;
- stale Function reference rejected with `SESSION_STALE`;
- clean MCP probe exit and clean LegacyHost Ctrl+C shutdown.

## Gate C real workstation evidence — PASS

Date: 2026-10-27.

Disposable project:

```text
УСПД Жуковский ПНС Gate C
```

Selected Function:

```text
===+++УСПД_Э3#-A1
```

Original displayed Function Text:

```text
Контроллер УСПД БКД-ПК-RF.2
```

Temporary text:

```text
Audeu Gate C TEST
```

Accepted sequence:

```text
PASS: MCP discovery exposes exactly eight approved Task 12 tools
PASS: eplan_status through real MCP stdio client
PASS: disposable project guard accepted 'УСПД Жуковский ПНС Gate C'
PASS: selected Function: ===+++УСПД_Э3#-A1
PASS: captured original Function Text: 'Контроллер УСПД БКД-ПК-RF.2'
PASS: preview created immutable plan
PASS: preview is EPLAN-read-only; original Function Text is unchanged
PASS: apply by changePlanId changed exactly the previewed Function Text
PASS: undo restored the exact original Function Text
PASS: controlled pre-commit failure rolled back and preserved the original Function Text
PASS: Gate C real MCP-to-EPLAN preview/apply/undo/rollback acceptance completed successfully
PASS: Task 12 Gate C probe exited cleanly
PASS: LegacyHost returned cleanly to PowerShell after Ctrl+C with no FAIL/exception
```

Gate C proves the real end-to-end path:

```text
MCP -> named pipe -> LegacyHost -> EPLAN RemoteClient -> AddIn -> EPLAN
```

for immutable preview, apply-by-plan-ID, read-back verification, undo and transaction rollback.

## Immediate next actions

1. Begin Task 13 / Gate D from current `main` only.
2. Perform final v0.1 verification against the integrated eight-tool surface.
3. Prepare release/setup documentation and packaging artifacts.
4. Confirm clean install/startup/shutdown procedure for the target EPLAN 2.9.4 workstation.
5. Preserve the closed-world v0.1 boundary; do not add arbitrary EPLAN write capabilities during packaging.

## Release 0.1 boundaries

Included:

- status/connection;
- active project;
- page list;
- current selection;
- Function inspection;
- one Function Text Preview -> Apply -> Verify -> Undo/Rollback workflow.

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
3. Prefer small branches/PRs and explicit gate evidence.
4. Never test mutating operations on a production EPLAN project.
5. No arbitrary code, property ID, EPLAN action name or filesystem path may be model-controlled.
6. Durable object identity is never inferred from session-scoped EPLAN IDs.
7. Require non-zero executed test counts for CI evidence.
8. Do not merge future project PRs without explicit user approval.
