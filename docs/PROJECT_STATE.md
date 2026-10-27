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
- mutating work is previewed, represented as an immutable Change Plan, applied by plan ID only, verified, and reversible.

## Approved documents

Core design / plan:

- `docs/design/eplan-ai-bridge-v0.1.md`
- `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- `docs/setup/eplan-2.9-prerequisites.md`

M0 read-only bridge:

- `docs/design/task-8-mcp-readonly-tools.md`
- `docs/plans/2026-10-26-task-8-mcp-readonly-tools.md`
- `docs/design/task-9-gate-b-workstation-acceptance.md`
- `docs/plans/2026-10-26-task-9-gate-b-workstation-acceptance.md`
- `docs/setup/task9-gate-b-validation.md`

M1 safe write:

- `docs/superpowers/specs/2026-10-27-task-10-change-plan-design.md`
- `docs/superpowers/plans/2026-10-27-task-10-change-plan.md`
- `docs/superpowers/specs/2026-10-27-task-11-safe-function-text-design.md`
- `docs/superpowers/plans/2026-10-27-task-11-safe-function-text.md`
- `docs/superpowers/specs/2026-10-27-task-12-gate-c-design.md`
- `docs/superpowers/plans/2026-10-27-task-12-gate-c.md`
- `docs/setup/task12-gate-c-validation.md`

## Verified target workstation

Confirmed through real build/runtime evidence on 2026-10-26 and 2026-10-27:

- EPLAN Electric P8 Professional 64-bit;
- version 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting DLLs present;
- x64 .NET SDK 8.0.427 available under `C:\Program Files\dotnet`;
- workstation PATH also contains `C:\Program Files (x86)\dotnet` before x64, so workstation scripts prepend the x64 host;
- AddIn compiles against the real EPLAN 2.9.4 assemblies and loads in EPLAN;
- fixed action `AudeuAiDispatch` registers and executes;
- real RemoteClient discovery/connect/ping succeeds;
- real persistent named-pipe read sequence succeeds;
- official MCP stdio client reaches EPLAN end-to-end through all bridge layers;
- real Function Text read/write/undo/rollback behavior has been accepted through Gate C.

## Gate status

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime / real DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly/runtime compatibility:** PASS.
- **Tasks 1–9 / M0 read-only bridge:** PASS and integrated to `main`.
- **Gate B / Task 9 real MCP-to-EPLAN read-only acceptance:** PASS and integrated to `main`.
- **Task 10 immutable Change Plan lifecycle:** REPO-SIDE PASS; PR #25 ready, unmerged pending explicit approval.
- **Task 11 internal safe Function Text write boundary:** REPO-SIDE PASS; PR #27 ready, unmerged pending explicit approval.
- **Task 12 MCP preview/apply/undo surface:** **PASS**; Issue #28 completed; PR #29 ready, unmerged pending explicit approval.
- **Gate C / M1 safe write:** **PASS on real EPLAN 2.9.4** using a disposable project copy.
- **Gate D / Task 13 final verification and v0.1 packaging:** NOT STARTED.

## Current status

- [x] Task 1: repository scaffold / CI.
- [x] Task 2: versioned contracts.
- [x] Task 3: secure local spool.
- [x] Task 4: AddIn + fixed `AudeuAiDispatch`.
- [x] Task 5: project session + read-only EPLAN inspection.
- [x] Task 6: LegacyHost RemoteClient gateway.
- [x] Task 7: secured current-user named-pipe LegacyHost.
- [x] Task 8: .NET 8 stdio MCP read-only tools — merged to `main` via PR #21.
- [x] Task 9 / Gate B: real MCP-to-EPLAN read-only acceptance — PASS and merged via PR #23.
- [x] Task 10: immutable MCP Change Plan state machine for `setFunctionText` — repo-side complete, PR #25.
- [x] Task 11: internal safe Function Text apply/undo path in AddIn/EPLAN boundary — repo-side complete, PR #27.
- [x] Task 12 / Gate C: MCP preview/apply/undo tools plus real safe-write acceptance — PASS, Issue #28 completed, PR #29.
- [ ] Task 13 / Gate D: final verification, integration and v0.1 packaging.

## Integrated M0 history

Tasks 1–7 were integrated to `main` at merge commit `1064c841d0b33f29b649b03c7dd902fd3f4a7bbd`; main CI #239 passed after integration.

Tasks 8–9 completed M0 integration on 2026-10-27:

- PR #21 -> `main` merge commit `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`; main CI #303 PASS; Issue #20 completed.
- PR #23 -> `main` merge commit `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`; main CI #304 PASS; PowerShell Encoding Guard #31 PASS; Issue #22 completed.

## Gate B real workstation evidence — PASS

Project used during Gate B: `УСПД Жуковский ПНС`, 7 pages.

Selected Function:

```text
===+++УСПД_Э3#-A1
```

Displayed Function Text:

```text
Контроллер УСПД БКД-ПК-RF.2
```

Accepted sequence:

```text
PASS: MCP discovery exposes exactly five approved read-only tools
PASS: eplan_status through real MCP stdio client
PASS: eplan_get_active_project
PASS: eplan_list_pages through MCP
PASS: eplan_get_selection through MCP
PASS: eplan_get_object returned exact displayed Function Text
PASS: closed-project read returned NO_ACTIVE_PROJECT and cleared old session
PASS: reopened project established a fresh session
PASS: old Function reference rejected with SESSION_STALE
PASS: Gate B real MCP-to-EPLAN read-only acceptance completed successfully
PASS: Task 9 Gate B probe exited cleanly
PASS: LegacyHost clean Ctrl+C shutdown
```

Real-acceptance defects found and fixed during Gate B:

1. Windows PowerShell 5.1 Cyrillic script decoding.
2. EPLAN multilingual Function Text internal representation (`??_??@...;`) vs displayed text.
3. Official MCP SDK error wrapper around stable bridge error codes.

## Task 10 — immutable MCP Change Plan lifecycle — REPO-SIDE PASS

Canonical issue: #24. Branch: `m1/task-10-change-plans`. PR: #25 (ready for review; unmerged pending explicit approval).

Implemented:

- defensive snapshot of all Function target identity fields;
- stable `PLAN_NOT_FOUND`, `PLAN_STALE`, `PLAN_ALREADY_APPLIED` behavior;
- synchronized in-memory Change Plan store;
- atomic single-use claim for apply;
- terminal `previewed -> applied|failed` lifecycle;
- planner snapshots target before its first await;
- maximum Function Text length 1024 UTF-16 code units;
- preview performs exactly one existing `object.get` read operation;
- old displayed Function Text is captured and stored with proposed new text;
- malformed/non-Function/unknown responses fail closed;
- no EPLAN write path and no new external MCP write tool were introduced in Task 10 itself.

Final repo-side Task 10 evidence included Contracts **20/20**, LegacyHost **45/45**, AddIn **32/32**, MCP **38/38**, MCP probe **6/6**, plus build and prerequisite guards.

## Task 11 — internal safe Function Text write boundary — REPO-SIDE PASS

Canonical issue: #26. Branch: `m1/task-11-safe-function-text`. PR: #27 (stacked on Task 10; ready for review; unmerged pending explicit approval).

Fixed internal bridge operations:

- `change.apply`;
- `change.undoLast`.

Safety properties:

- read and write services share one long-lived `ProjectSessionTracker`;
- write requests require `Mode = write` and a current live project session;
- read-only project/Function state fails before mutation;
- `change.apply` accepts only the fixed Change Plan payload generated by MCP;
- Change Plan ID is claimed before mutation and never made retryable after an uncertain write outcome;
- target resolution rechecks project/database/type/object/string identity and Function type;
- displayed old `FUNC_TEXT` must exactly match the previewed old value or apply fails `PLAN_STALE`;
- the only property mutated is Function `FUNC_TEXT`;
- persistent changes are bounded by `UndoStep`, `SafetyPoint` and `Transaction`;
- write is read back and verified before commit;
- uncertain implicit transaction commit fails closed;
- exception/verification failure before commit leaves SafetyPoint uncommitted and rolls back;
- undo uses a guarded last-change snapshot rather than retaining a disposable EPLAN UndoStep across calls;
- arbitrary operations remain rejected.

Final Task 11 code evidence: Contracts **20/20**, LegacyHost **45/45**, AddIn **60/60**, MCP **38/38**, MCP probe **6/6**, builds, PowerShell 5.1 parse validation and EPLAN prerequisite guards PASS.

## Task 12 — MCP safe write surface — PASS

Canonical issue: #28 (completed). Branch: `m1/task-12-gate-c`. PR: #29 (stacked on Task 11; ready for review; unmerged pending explicit approval).

Exactly three approved model-callable tools were added:

- `eplan_preview_set_function_text`;
- `eplan_apply_change_plan`;
- `eplan_undo_last_ai_change`.

Closed-world input rules:

- preview accepts one compact Function reference plus proposed text;
- apply accepts **only** `changePlanId`;
- undo accepts **only** `projectSessionId`;
- no caller can resubmit or alter target/old/new values during apply;
- no arbitrary property IDs, bridge operation names, EPLAN actions, filesystem paths or code are exposed.

Gate C workstation automation:

- `scripts/test-task12-gate-c.ps1`;
- exact double disposable-project-name confirmation;
- exactly one writable Function must be selected;
- temporary Function Text must be non-empty and <= 1024 UTF-16 code units;
- rollback test uses a fixed one-shot same-user marker `%TEMP%\Audeu.EplanAi.GateC.fail-next-write`;
- marker is not an MCP parameter and cannot be used for arbitrary mutation;
- marker is consumed before the deliberate pre-commit failure and cleaned in `finally`.

Final repo-side Task 12 code head before workstation evidence:

```text
5a70254bc7328de488d67ba9e087603cd64f3559
```

Repo-side verification:

- CI #354 SUCCESS;
- PowerShell Encoding Guard #81 SUCCESS;
- Contracts **20/20 PASS**;
- LegacyHost **45/45 PASS**;
- AddIn **64/64 PASS**;
- MCP **54/54 PASS**;
- MCP probe **17/17 PASS**;
- MCP/probe builds PASS;
- Windows PowerShell 5.1 parse validation PASS;
- AddIn/LegacyHost prerequisite guards PASS.

Gate C evidence commit `92f86ddca882004c04311309b575e81d98d0bc33` was then verified by CI #355 SUCCESS and PowerShell Encoding Guard #82 SUCCESS.

## Gate C real workstation acceptance — PASS

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

Temporary test text:

```text
Audeu Gate C TEST
```

Accepted real sequence:

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

This proves on the real EPLAN 2.9.4 workstation that:

- preview does not mutate EPLAN;
- apply uses the immutable plan and changes exactly the intended Function Text;
- read-back observes the applied value;
- undo restores the exact original value;
- a deliberate failure after assignment but before commit rolls back to the exact original value;
- the full path MCP -> pipe -> LegacyHost -> RemoteClient -> AddIn -> EPLAN is operational for the approved write flow;
- the persistent LegacyHost/RemoteClient path shuts down cleanly after acceptance.

## Immediate next actions

1. Keep PR #25, PR #27 and PR #29 unmerged until explicit user approval.
2. After approval, integrate the stacked M1 chain in order: Task 10 -> Task 11 -> Task 12, retargeting/checking each PR against the newly updated base and requiring fresh CI after each merge.
3. Start Task 13 / Gate D only after M1 integration: final verification, release documentation and v0.1 packaging.
4. Preserve the closed-world rule; do not expand v0.1 into arbitrary EPLAN writes.

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
3. Prefer small stacked branches/PRs.
4. Never test mutating operations on a production EPLAN project.
5. No arbitrary code, property ID, EPLAN action name or filesystem path may be model-controlled.
6. Durable object identity is never inferred from session-scoped EPLAN IDs.
7. Require non-zero executed test counts for CI evidence.
8. Do not merge project PRs without explicit user approval.
