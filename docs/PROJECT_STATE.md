# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-26

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

Key rule: the LLM does not receive unrestricted EPLAN API access. It works through a small, versioned bridge/domain API with validation and explicit Change Plans.

## Approved documents

- Technical design: `docs/design/eplan-ai-bridge-v0.1.md`
- Implementation plan: `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- Architecture decision: `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- Gate A procedure: `docs/setup/eplan-2.9-prerequisites.md`
- Task 4 local validation: `docs/setup/task4-local-validation.md`

## Verified target workstation

Confirmed on 2026-10-23 and extended with real build evidence on 2026-10-26:

- EPLAN Electric P8 Professional (64-bit);
- version 2.9 SP1 Update 4;
- build 14773;
- EPLAN API / API Extension available;
- `EplanApi29Dir = C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- required add-in DLLs present: `Eplan.EplApi.AFu.dll`, `Baseu.dll`, `DataModelu.dll`, `HEServicesu.dll`;
- required Remoting DLLs present: `Eplan.EplApi.RemoteClientu.dll`, `Remotingu.dll`;
- .NET SDK x64 available under `C:\Program Files\dotnet` (SDK 8.0.427 confirmed);
- `Audeu.EplanAi.AddIn` now compiles successfully against the real EPLAN 2.9.4 API assemblies.

Gate A is split into:

- **Gate A1 — development environment:** PASS;
- **Gate A2 — runtime integration:** PARTIAL. Real AddIn build is PASS; registration/load/action round-trip are still pending. RemoteClient remains Task 6.

Gate B / M0 still requires all relevant Gate A2 checks to pass.

## Current status

- [x] Repository and foundation documentation established.
- [x] EPLAN AI Bridge 0.1 architecture approved.
- [x] Sequential implementation plan M0 → M1 approved.
- [x] Repository designated as source of truth and long-term project memory.
- [x] Gate A1: real EPLAN 2.9 development environment validated.
- [~] Gate A2: real AddIn build PASS; registration/load/direct action test and later RemoteClient test still pending.
- [x] Task 1: repository scaffold and CI — branch `m0/task-1-scaffold`, draft PR #3.
- [x] Task 2: versioned bridge contracts — branch `m0/task-2-contracts`, issue #4 closed, draft stacked PR #5.
- [x] Task 3: secure local spool — branch `m0/task-3-spool`, issue #6 closed, draft stacked PR #7.
- [~] Task 4: minimal AddIn + fixed `AudeuAiDispatch` implemented, CI-green, and real EPLAN 2.9.4 build PASS; Issue #8 remains open pending registration/load and status round-trip.
- [ ] Task 5: project session and read-only EPLAN operations.
- [ ] Task 6: LegacyHost RemoteClient gateway.
- [ ] Gate B / M0: prove read-only bridge on real EPLAN 2.9.
- [ ] Gate C / M1: prove safe Function Text Preview → Apply → Verify → Undo/Rollback.

## Test runner ruling

The project uses xUnit v3. Test projects are executable test applications. With the .NET 8 SDK used in CI, `dotnet test` is routed through Microsoft Testing Platform with:

```xml
<OutputType>Exe</OutputType>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
```

A green workflow step alone is not test evidence. Completion evidence must include a runner summary with a non-zero executed-test count.

## Task 1 — scaffold

Created the solution/project shells, shared MSBuild configuration, central package versions, EPLAN API-path guards, MCP/Contracts shells and Windows CI.

Evidence:

- CI run `39959463954` — success;
- Contracts and MCP build independently of EPLAN;
- AddIn and LegacyHost intentionally fail with a clear prerequisite message when `EplanApi29Dir` is absent.

Ruling: `Audeu.EplanAi.LegacyHost.Tests` targets `net472` because it references a `net472` project. Contracts/MCP tests remain `net8.0`; LegacyHost/AddIn integration tests are `net472`.

## Task 2 — versioned bridge contracts

Implemented:

- protocol version `1.0` and explicit compatibility check;
- `BridgeRequest` / `BridgeResponse`;
- session-scoped `ObjectReference`;
- initial stable error codes;
- `ChangeOperation` and immutable-identity `ChangePlan`;
- state transitions only `previewed -> applied|failed`.

Verified branch-head evidence:

- CI run `39975775380` — SUCCESS;
- Contracts: **18 passed, 0 failed, 0 skipped**;
- MCP build passed;
- both negative EPLAN API guards passed.

No EPLAN assembly or serializer package dependency was added to `Audeu.EplanAi.Contracts`.

## Task 3 — secure local spool

Implemented:

- fixed root `%LOCALAPPDATA%\Audeu\EplanAiBridge`;
- canonical GUID request/response filenames;
- request limit 1 MiB and response limit 16 MiB;
- request temp-write + same-directory rename;
- duplicate request fail-closed behavior;
- typed response read + RequestId verification;
- stale request/response cleanup;
- test-only `SkipEplanApiValidation=true` for EPLAN-independent LegacyHost unit tests while ordinary runtime builds still require `EplanApi29Dir`.

TDD/verification evidence:

- valid RED run `39975854174` after 18/18 Contracts passed; spool tests then failed on missing production namespaces;
- implementation commit `08e386f9e690dc8dece51c96a20b659dd49c0512`;
- synchronized branch-head run `39976839719` — SUCCESS;
- PR run `39976847095` — SUCCESS;
- Contracts: **18/18**;
- LegacyHost spool: **8/8**;
- MCP build and both EPLAN guards passed.

## Task 4 — minimal AddIn and fixed dispatch action

Canonical tracking: Issue #8. Issues #9 and #10 are closed duplicates created by repeated API invocations and contain no project work.

Branch: `m0/task-4-dispatch`, based on Task 3 head.

Repo-side implementation:

- exactly one `EplanAddIn : IEplAddIn`;
- exactly one `AudeuAiDispatchAction : IEplAction`;
- fixed EPLAN action name `AudeuAiDispatch`, ordinal `20`;
- only `REQUESTID` is consumed by the EPLAN action;
- `REQUESTID` must be canonical GUID `D` format;
- malformed/missing IDs fail closed;
- AddIn reads request from fixed spool and writes matching response to fixed spool;
- operation registry is an explicit whitelist;
- first whitelisted operation is `system.status`;
- unknown operation returns `BRIDGE_UNKNOWN_OPERATION`;
- protocol mismatch returns `PROTOCOL_MISMATCH`;
- no bridge operation is forwarded as an arbitrary EPLAN action name;
- no arbitrary filesystem path enters the dispatch interface.

### Task 4 test-only EPLAN API stubs ruling

GitHub runners do not have the licensed EPLAN 2.9 API assemblies. For the thin EPLAN adapter tests only, `UseEplanApiStubs=true` compiles a test-support file containing only the EPLAN 2.9 signatures used by Task 4 (`IEplAddIn`, `IEplAction`, `ActionCallingContext`, `ActionProperties`).

This switch is opt-in and test-only. Normal AddIn builds still require the real `EplanApi29Dir`; CI continues verifying that the production guard is intact. Stub-backed CI therefore proves bridge logic and adapter shape, not binary compatibility with the actual workstation assemblies.

The 2026-10-26 real-workstation build exposed an important stub mismatch: the installed EPLAN 2.9.4 `ActionProperties` does not expose `Description`. The stub was corrected to match the real assembly shape and `GetActionProperties(ref ActionProperties)` was changed to a no-op. This is now covered by regression TDD.

### Task 4 TDD evidence

Initial RED/GREEN:

- RED commit `7d4ea1a75ce4d57b845627744826b3c57ba0d34b`;
- CI run `40017002588`;
- Contracts **18/18** and spool **8/8** passed first;
- AddIn step failed specifically because production `Actions`, `Dispatch`, `Serialization` and `AudeuAiDispatchAction` were absent;
- implementation commit `ded33eec743cdb444fac79a2e9af9e35a276ee59`;
- initial GREEN CI `40017268214` — SUCCESS;
- local-validation/docs commit `77fb10167f90d3f52679abcb27c02fdbf12e1175`;
- repo-side verification CI `40017826313` — SUCCESS.

Real-API regression RED/GREEN on 2026-10-26:

- workstation real build reached EPLAN 2.9.4 assemblies and failed with CS1061 at `AudeuAiDispatchAction.cs` because `ActionProperties.Description` is unavailable;
- RED commit `f1965572044651ca5e5fa78b10b7017916bdb586` corrected the stub shape and reproduced the same CS1061 in CI run `40293424745`;
- GREEN fix commit `11f77768bf21e580b7d9ec284ab73647985e30db` removed the unavailable member access while retaining the required interface method;
- GREEN CI run `40293650296` — SUCCESS;
- Contracts: **18/18**;
- LegacyHost spool: **8/8**;
- AddIn dispatch: **10/10**;
- MCP build: success;
- ordinary AddIn and LegacyHost negative EPLAN API guards: success.

### Task 4 real-workstation validation

**Real AddIn build: PASS on 2026-10-26.**

Command:

```powershell
.\scripts\verify-task4-build.ps1
```

Verified output:

```text
Audeu.EplanAi.Contracts netstandard2.0 ... success
Audeu.EplanAi.AddIn net472 ... success
PASS: real-API AddIn build produced ...\src\Audeu.EplanAi.AddIn\bin\Release\net472\Audeu.EplanAi.AddIn.dll
```

This confirms compilation against the actual EPLAN 2.9.4 API assemblies in `C:\Program Files\EPLAN\Platform\2.9.4\Bin`.

Still required:

```powershell
.\scripts\verify-task4-build.ps1 -Register
```

Then prove the fixed action + spool round-trip:

```powershell
.\scripts\test-task4-status-roundtrip.ps1
```

Expected final output includes:

```text
PASS: AudeuAiDispatch system.status round-trip succeeded for <guid>
```

Until registration/load and this real-workstation round-trip pass, Task 4 and the relevant Gate A2 portion remain open. RemoteClient validation remains Task 6.

## Immediate next actions

1. Do not merge PR #3, #5, #7 or the Task 4 PR without explicit user approval.
2. Register/load the real AddIn via `scripts\verify-task4-build.ps1 -Register` and preserve full output / any EPLAN message.
3. Run `scripts\test-task4-status-roundtrip.ps1` and preserve PASS/error output.
4. Record registration/load and direct action Gate A2 evidence in Issue #8 and this file.
5. Only then advance to Task 5 read-only DataModel work; do not start M1 write work until Gate B / M0 passes.

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
9. Do not merge project PRs without explicit user approval.
