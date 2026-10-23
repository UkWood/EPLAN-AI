# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN 2.9  
**Current release target:** `0.1`  
**Updated:** 2026-10-23

## Purpose of this file

This is the living restart point for the project. Update it whenever a meaningful implementation step, architectural decision, blocker, test result, or milestone changes. A future worker should be able to read this file plus the linked design/plan and continue without relying on chat history.

## Approved architecture

```text
ChatGPT / MCP client
        |
        v
Audeu.EplanAi.Mcp            (.NET 8)
        |
        | Named Pipe
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

Key rule: the LLM does not receive unrestricted EPLAN API access. It works through a small, versioned domain/bridge API with validation and explicit Change Plans.

## Approved documents

- Technical design: `docs/design/eplan-ai-bridge-v0.1.md`
- Implementation plan: `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- Architecture decision: `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- Gate A procedure: `docs/setup/eplan-2.9-prerequisites.md`

## Current status

- [x] Repository created.
- [x] EPLAN 2.9 API documentation studied at architecture level.
- [x] EPLAN AI Bridge 0.1 architecture approved.
- [x] Sequential implementation plan M0 → M1 approved.
- [x] Repository designated as the source of truth and long-term project memory.
- [x] Project foundation documentation merged to `main` via PR #1.
- [ ] Gate A: validate the real EPLAN 2.9 development environment.
- [~] Task 1: solution scaffold and prerequisite checks — implementation branch `m0/task-1-scaffold`; local EPLAN checks still pending.
- [ ] Task 2: versioned bridge contracts.
- [ ] Task 3: secure local spool.
- [ ] Task 4: load add-in and round-trip `system.status` through `AudeuAiDispatch`.
- [ ] Gate B / M0: prove read-only bridge on real EPLAN 2.9.
- [ ] Gate C / M1: prove safe Function Text Preview → Apply → Verify → Undo/Rollback.

## Task 1 implementation state

Created on `m0/task-1-scaffold`:

- `EplanAiBridge.sln`;
- shared MSBuild configuration;
- central package versions;
- `netstandard2.0` Contracts shell;
- `net472` AddIn shell with explicit EPLAN DLL guard;
- `net472` LegacyHost shell with explicit Remoting DLL guard;
- `net8.0` MCP shell;
- four test-project shells;
- Gate A prerequisite documentation;
- Windows CI for non-EPLAN builds and negative EPLAN-path guard tests.

### Ruling

`Audeu.EplanAi.LegacyHost.Tests` targets `net472`, not `net8.0`, because it directly references a `net472` project. Contracts/MCP tests remain `net8.0`; LegacyHost/AddIn integration tests are `net472`. The implementation plan has been corrected accordingly.

## Immediate next actions

1. Run/confirm GitHub CI for the Task 1 PR.
2. On the target Windows/EPLAN workstation, complete the Gate A table in `docs/setup/eplan-2.9-prerequisites.md`:
   - exact EPLAN 2.9 build/version;
   - API/API Extension entitlement;
   - exact `EplanApi29Dir`;
   - required AddIn/Remoting DLLs present;
   - minimal add-in load (Task 4);
   - RemoteClient connection (Task 4/6).
3. Do not mark Task 1 / Gate A fully complete until the workstation-dependent checks are evidenced.

If API licensing/assemblies are unavailable, stop at Gate A and re-scope. Do not silently replace the architecture with GUI automation.

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
