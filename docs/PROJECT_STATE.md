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

Tasks 2–3 do not depend on runtime integration code that does not exist yet. Gate B / M0 still requires Gate A2 to pass.

## Current status

- [x] Repository and foundation documentation established.
- [x] EPLAN AI Bridge 0.1 architecture approved.
- [x] Sequential implementation plan M0 → M1 approved.
- [x] Repository designated as source of truth and long-term project memory.
- [x] Gate A1: real EPLAN 2.9 development environment validated.
- [ ] Gate A2: add-in load / dispatch / RemoteClient runtime integration.
- [x] Task 1: repository scaffold and CI — branch `m0/task-1-scaffold`, draft PR #3.
- [x] Task 2: versioned bridge contracts — branch `m0/task-2-contracts`, issue #4 closed, draft stacked PR #5.
- [x] Task 3: secure local spool — branch `m0/task-3-spool`, issue #6, draft stacked PR #7.
- [ ] Task 4: minimal add-in + fixed dispatch action `AudeuAiDispatch`.
- [ ] Task 5: project session and read-only EPLAN operations.
- [ ] Task 6: LegacyHost RemoteClient gateway.
- [ ] Gate B / M0: prove read-only bridge on real EPLAN 2.9.
- [ ] Gate C / M1: prove safe Function Text Preview → Apply → Verify → Undo/Rollback.

## Task 1 — scaffold

Created:

- `EplanAiBridge.sln`;
- shared MSBuild configuration;
- central package versions;
- `netstandard2.0` Contracts shell;
- `net472` AddIn and LegacyHost shells with explicit EPLAN DLL guard;
- `net8.0` MCP shell;
- test project shells;
- Windows CI and Gate A prerequisite documentation.

CI run `39959463954` passed the non-EPLAN builds and both intentional negative EPLAN-path guard checks.

Ruling: `Audeu.EplanAi.LegacyHost.Tests` targets `net472`, because it directly references a `net472` project. Contracts/MCP tests remain `net8.0`; LegacyHost/AddIn integration tests are `net472`.

## Test runner ruling

The project uses xUnit v3. Test projects are executable test applications. With the .NET 8 SDK used in CI, `dotnet test` is routed through Microsoft Testing Platform with:

```xml
<OutputType>Exe</OutputType>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
```

This ruling was added after log inspection showed that the earlier VSTest configuration could exit successfully while printing `No test is available`.

A green workflow step alone is not test evidence. Completion evidence must include a runner summary with a non-zero executed-test count.

## Task 2 — versioned bridge contracts

Implemented:

- protocol version `1.0` and explicit compatibility check;
- `BridgeRequest` / `BridgeResponse` envelopes with deterministic defaults;
- session-scoped `ObjectReference` using `uint` database/type IDs and `long` object ID;
- initial stable error-code constants;
- `ChangeOperation`;
- immutable-identity `ChangePlan` with defensive operation-list copy;
- state machine permitting only `previewed -> applied|failed`.

Original TDD sequence:

- protocol RED commit `55ee47135c018e4050e908fe070ce1ee9cd16895`, run `39972910562`;
- protocol implementation commit `aa4a6f80eb802e72196398fa5afbc079ba0d863b`;
- Change Plan RED commit `83667208c927c77f51268250145c4c615f02dd01`, run `39973445201`;
- Change Plan implementation commit `7644502a66d828be20fec8a14cdabe33501eb10b`.

Corrected verification after the test-runner ruling:

- CI run `39975775380` on branch head `02ce54dbb6dbf04ebff6ff4fdd8113ccc3bd8fae` — SUCCESS;
- actual Contracts summary: **18 passed, 0 failed, 0 skipped**;
- MCP build passed;
- both negative EPLAN API guards passed.

No EPLAN assembly or serializer package dependency was added to `Audeu.EplanAi.Contracts`.

## Task 3 — secure local spool

Implemented:

- fixed default root `%LOCALAPPDATA%\Audeu\EplanAiBridge`;
- `requests` / `responses` subdirectories;
- filenames derived only from typed `Guid` request IDs;
- canonical `D` GUID formatting;
- request payload limit **1 MiB**;
- response payload limit **16 MiB**;
- request writes to `*.tmp` followed by same-directory rename;
- duplicate request ID fails closed instead of overwriting an existing request;
- temp cleanup on failed write;
- typed `BridgeResponse` deserialization;
- response `RequestId` must match the requested ID;
- stale request/response cleanup based on UTC file time;
- test-only `SkipEplanApiValidation=true` path so EPLAN-independent LegacyHost infrastructure can be unit-tested on GitHub runners;
- ordinary LegacyHost builds still enforce `EplanApi29Dir`.

TDD evidence:

- run `39974710973` is **not** behavioral RED; it exposed the xUnit v3 runner problem before spool tests executed;
- valid RED run `39975854174`: Contracts executed and passed **18/18**, then LegacyHost tests failed exactly on missing production namespaces `Configuration` and `Spool` (`CS0234`);
- GREEN implementation commit `08e386f9e690dc8dece51c96a20b659dd49c0512`;
- GREEN run `39976372025`: Contracts **18/18**, LegacyHost spool **8/8**, MCP build success, AddIn guard success, ordinary LegacyHost guard success.

Task 3 therefore has real executed-test evidence, not compilation-only evidence.

## Immediate next actions

1. Do not merge PR #3, #5, or #7 without explicit user approval.
2. Start Task 4 from the Task 3 head: minimal EPLAN add-in lifecycle plus fixed `AudeuAiDispatch` action.
3. Task 4 must produce the first code that can be built against the verified local EPLAN 2.9 API assemblies.
4. Execute Gate A2 checks on the real workstation as soon as Task 4/6 make them possible.
5. Do not mark Gate B / M0 complete until add-in load, action registration and RemoteClient round-trip pass on the verified workstation.

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
