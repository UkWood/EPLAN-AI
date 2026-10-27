# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1.0`  
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
        | fixed current-user named pipe
        v
Audeu.EplanAi.LegacyHost     (.NET Framework 4.7.2)
        |
        | EPLAN RemoteClient / fixed AudeuAiDispatch
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
- mutating work is previewed, represented as an immutable Change Plan, applied by plan ID only, verified and reversible;
- no TCP/HTTP listener exists in v0.1.

## Verified workstation

Confirmed through real build/runtime evidence on 2026-10-26 and 2026-10-27:

- EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773;
- API/API Extension present;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn/DataModel/HEServices and RemoteClient/Remoting assemblies present;
- x64 .NET 8 SDK available under `C:\Program Files\dotnet`;
- AddIn compiles against the real EPLAN 2.9.4 assemblies and loads in EPLAN;
- fixed action `AudeuAiDispatch` registers and executes;
- real RemoteClient discovery/connect/ping succeeds;
- official MCP stdio client reaches EPLAN end-to-end through all bridge layers;
- real read-only and safe Function Text write/undo/rollback flows passed Gate B and Gate C.

## Gate status

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime / real DataModel compatibility:** PASS.
- **Gate A2 — RemoteClient real assembly/runtime compatibility:** PASS.
- **Gate B / M0 read-only bridge:** PASS and integrated to `main`.
- **Gate C / M1 safe write:** PASS and integrated to `main`.
- **Task 13 repo-side packaging/security implementation:** IN PROGRESS on branch `m1/task-13-gate-d`, draft PR #31.
- **Gate D / final real-EPLAN release verification:** PENDING workstation run of `scripts\verify-task13-gate-d.ps1`.

Gate D must not be marked PASS until the verified EPLAN 2.9.4 workstation builds the release bundle, verifies its hashes, and that evidence is recorded.

## Current status

- [x] Tasks 1–9 / M0 read-only bridge and Gate B.
- [x] Tasks 10–12 / M1 safe Function Text write and Gate C.
- [x] Task 13 design and implementation plan.
- [x] Task 13 reproducible release packager + package-layout/checksum contract.
- [x] Task 13 automated security verifier + positive/negative fixture contract.
- [x] Task 13 fail-closed release builder guards.
- [x] Task 13 real-EPLAN release builder and final Gate D workstation verifier implemented.
- [x] v0.1 installation, troubleshooting, security and release-note documentation drafted.
- [ ] Final fresh branch CI after all Task 13 documentation/workflow integration.
- [ ] Real workstation Gate D build/package/hash verification.
- [ ] Record Gate D evidence, mark PR #31 ready, then await explicit merge approval.
- [ ] After merge and separate explicit release approval, create tag `eplan-ai-bridge-v0.1.0`.

## Integration history

### M0

Tasks 1–7 integrated at merge commit `1064c841d0b33f29b649b03c7dd902fd3f4a7bbd`.

- PR #21 / Task 8 -> `7c84ccfeda6f4ae689375ebdab21486b37b88d2c`; main CI #303 PASS.
- PR #23 / Task 9 / Gate B -> `fec3eb06708ef4f6906b1daa10d0222b056bd0d1`; main CI #304 PASS; Encoding Guard #31 PASS.

### M1

- PR #25 / Task 10 -> `3f81afa39ab51efe1ec48f48c20035ab2dd4d594`; main CI #359 PASS; Encoding Guard #86 PASS.
- PR #27 / Task 11 -> `5243179757989d4bf10e19ff53e71fb07a34ecfb`; main CI #360 PASS.
- PR #29 / Task 12 / Gate C -> `e3af813b7d238f5d44eb3f41ea438c032c0ae3d9`; main CI #361 PASS; Encoding Guard #88 PASS.
- final M1 project-state commit on `main`: `0aab3adf6fa14191f76dd7b875cec3a93ada915d`; CI #362 PASS; Encoding Guard #89 PASS.

M0 + M1 are fully integrated into `main`.

## Current MCP surface

Exactly eight approved model-callable tools are exposed.

Read-only:

- `eplan_status`;
- `eplan_get_active_project`;
- `eplan_list_pages`;
- `eplan_get_selection`;
- `eplan_get_object`.

Safe Function Text workflow:

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

Gate B defects found/fixed during acceptance: Windows PowerShell Cyrillic decoding, EPLAN multilingual Function Text display conversion, and MCP SDK error wrapping around stable bridge errors.

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

This proves the real end-to-end write path:

```text
MCP -> named pipe -> LegacyHost -> EPLAN RemoteClient -> AddIn -> EPLAN
```

for immutable preview, apply-by-plan-ID, read-back verification, undo and transaction rollback.

## Task 13 / Gate D

Canonical issue: #30. Branch: `m1/task-13-gate-d`. Draft PR: #31. Do not merge or create a release tag without explicit user approval.

Approved Task 13 documents:

- `docs/superpowers/specs/2026-10-27-task-13-gate-d-design.md`;
- `docs/superpowers/plans/2026-10-27-task-13-gate-d.md`;
- `docs/release/v0.1/INSTALLATION.md`;
- `docs/release/v0.1/TROUBLESHOOTING.md`;
- `docs/release/v0.1/SECURITY.md`;
- `docs/release/v0.1/RELEASE_NOTES.md`.

### Release artifacts

The workstation builder produces under `artifacts\release\eplan-ai-bridge-v0.1.0`:

```text
Audeu.EplanAi.AddIn-0.1.0.zip
Audeu.EplanAi.LegacyHost-0.1.0.zip
Audeu.EplanAi.Mcp-0.1.0-win-x64.zip
EplanAiBridge-v0.1.0.zip
SHA256SUMS.txt
```

The combined archive contains `addin`, `legacyhost`, `mcp`, embedded release docs and `VERSION.txt`.

AddIn packaging rejects redistribution of EPLAN AF/Base/DataModel/HEServices DLLs. MCP is published framework-dependent for `win-x64` and .NET 8. LegacyHost remains a separate local gateway component.

### Task 13 automated verification evidence so far

TDD package cycle:

- RED CI #363: failed exactly because `New-Task13ReleaseBundle.ps1` did not exist;
- GREEN CI #364: package layout/checksum contract PASS and all pre-existing suites PASS.

TDD security cycle:

- RED CI #366: failed exactly because `verify-task13-security.ps1` did not exist;
- first implementation exposed a Windows PowerShell 5.1 parse defect (`$ErrorCode:` interpolation), root-caused and fixed without weakening the policy;
- GREEN CI #368: PowerShell 5.1 parse PASS, package contract PASS, security fixture contract PASS, current `src` security review PASS, all pre-existing suites and prerequisite guards PASS.

TDD release-guard cycle:

- RED Task 13 Gate D run #1: failed exactly because `build-task13-release.ps1` did not exist;
- GREEN Task 13 Gate D run #6 on head `99e18a611d590c7ef4ebb0274830d029dd45ad36`: release guard contract PASS.

The final release builder:

- requires the verified real EPLAN 2.9.4 API assembly set;
- refuses to build while EPLAN or persistent LegacyHost is running;
- uses x64 .NET 8;
- builds AddIn and LegacyHost from source against the real EPLAN API;
- explicitly restores and publishes MCP for `win-x64`;
- rejects forbidden EPLAN platform DLLs in AddIn staging;
- creates four versioned ZIPs plus a sorted SHA-256 manifest;
- independently recomputes every archive hash before reporting PASS.

`verify-task13-gate-d.ps1` also checks that Task 13 has not modified runtime source under `src/`. If runtime source changed after Gate C, it fails with `GATE_D_RUNTIME_REACCEPTANCE_REQUIRED` instead of reusing stale runtime acceptance evidence.

## Immediate next action

Complete fresh repo-side CI on the final Task 13 branch head. Then run real Gate D on the verified workstation with EPLAN and LegacyHost stopped:

```powershell
cd E:\EPLAN\REPO\EPLAN-AI
git switch m1/task-13-gate-d
git pull --ff-only origin m1/task-13-gate-d
.\scripts\verify-task13-gate-d.ps1
```

Expected final lines include:

```text
PASS: Gate D security review (...)
PASS: Task 13 v0.1 release bundle built and verified: ...
PASS: Gate D repository and real-EPLAN release verification completed successfully.
```

After that, record `SHA256SUMS.txt` and Gate D output in Issue #30 / PR #31 / this file. Only then may Gate D become PASS and PR #31 become ready for review.

## Release 0.1 boundaries

Included:

- status/connection;
- active project;
- page list;
- current selection;
- Function inspection;
- one Function Text Preview -> Apply -> Verify -> Undo/Rollback workflow;
- reproducible component/combined release packaging with SHA-256 integrity manifest.

Deferred:

- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- arbitrary EPLAN property/action access;
- network service deployment;
- MSI/MSIX/Setup.exe installer.

## Working rules

1. GitHub is project memory/source of truth.
2. Update this file after material implementation/evidence/blockers.
3. Prefer small branches/PRs and explicit gate evidence.
4. Never test mutating operations on a production EPLAN project.
5. No arbitrary code, property ID, EPLAN action name or filesystem path may be model-controlled.
6. Durable object identity is never inferred from session-scoped EPLAN IDs.
7. Require non-zero executed test counts for CI evidence.
8. Do not merge project PRs without explicit user approval.
9. Do not create the v0.1 release tag without separate explicit release approval.
