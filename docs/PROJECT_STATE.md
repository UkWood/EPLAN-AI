# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
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

## Verified target workstation

Confirmed on 2026-10-23:

- EPLAN Electric P8 Professional (64-bit);
- version 2.9 SP1 Update 4;
- build 14773;
- EPLAN API / API Extension available;
- `EplanApi29Dir = C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- required add-in DLLs present: `Eplan.EplApi.AFu.dll`, `Baseu.dll`, `DataModelu.dll`, `HEServicesu.dll`;
- required Remoting DLLs present: `Eplan.EplApi.RemoteClientu.dll`, `Remotingu.dll`.

Gate A is split into:

- **Gate A1 — development environment:** PASS;
- **Gate A2 — runtime integration:** pending until Tasks 4–6 provide code for add-in loading, action registration and RemoteClient round-trip.

This split is intentional: Task 2–3 do not depend on runtime integration code that does not exist yet, while Gate B / M0 still requires Gate A2 to pass.

## Current status

- [x] Repository created.
- [x] EPLAN 2.9 API documentation studied at architecture level.
- [x] EPLAN AI Bridge 0.1 architecture approved.
- [x] Sequential implementation plan M0 → M1 approved.
- [x] Repository designated as the source of truth and long-term project memory.
- [x] Project foundation documentation merged to `main` via PR #1.
- [x] Gate A1: real EPLAN 2.9 development environment validated.
- [ ] Gate A2: add-in load / dispatch / RemoteClient runtime integration.
- [x] Task 1 repository scaffold and CI completed on `m0/task-1-scaffold`; PR #3 remains open while runtime proof is tracked separately.
- [~] Task 2: versioned bridge contracts — next implementation task.
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

### CI evidence

GitHub Actions PR run `39959463954` completed successfully on 2026-10-23.

Verified steps:

- Contracts restore/build — success;
- MCP restore/build — success;
- AddIn without `EplanApi29Dir` — expected failure detected with the explicit Gate A message;
- LegacyHost without `EplanApi29Dir` — expected failure detected with the explicit Gate A message.

An earlier run exposed a CI harness defect: PowerShell retained `$LASTEXITCODE = 1` from the intentionally failing negative build even after the expected message was verified. The workflow was corrected to explicitly return success after a validated expected failure. The rerun is green.

### Ruling

`Audeu.EplanAi.LegacyHost.Tests` targets `net472`, not `net8.0`, because it directly references a `net472` project. Contracts/MCP tests remain `net8.0`; LegacyHost/AddIn integration tests are `net472`. The implementation plan has been corrected accordingly.

## Immediate next actions

1. Implement Task 2 on a dedicated branch from the Task 1 head: versioned bridge contracts and contract tests.
2. Follow RED → GREEN TDD for protocol and change-plan behavior.
3. Keep runtime checks for Gate A2 explicit and execute them when Task 4/6 make them possible.
4. Do not mark Gate B / M0 complete until add-in load, dispatch and RemoteClient round-trip pass on the verified workstation.

If API licensing/assemblies become unavailable, stop the EPLAN integration path and re-scope. Do not silently replace the architecture with GUI automation.

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
