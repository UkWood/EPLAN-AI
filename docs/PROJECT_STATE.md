# Project State

**Project:** EPLAN AI Bridge  
**Repository:** `UkWood/EPLAN-AI`  
**Target platform:** EPLAN Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773  
**Current release target:** `0.1`  
**Updated:** 2026-10-26

## Purpose

This is the living restart point for the project. Update it after every meaningful implementation step, architectural decision, blocker, test result, or milestone change.

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
        | EPLAN Remoting / WCF
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

Key rule: the LLM never receives unrestricted EPLAN API access. It works through a small versioned bridge/domain API with explicit validation and Change Plans.

## Approved documents

- Technical design: `docs/design/eplan-ai-bridge-v0.1.md`
- Implementation plan: `docs/plans/2026-10-23-eplan-ai-bridge-v0.1-implementation-plan.md`
- Architecture decision: `docs/decisions/0001-controlled-mcp-eplan-bridge.md`
- Gate A procedure: `docs/setup/eplan-2.9-prerequisites.md`
- Task 4 local validation: `docs/setup/task4-local-validation.md`

## Verified target workstation

Confirmed on 2026-10-23 and extended with real build/runtime evidence on 2026-10-26:

- EPLAN Electric P8 Professional 64-bit;
- version 2.9 SP1 Update 4, build 14773;
- EPLAN API / API Extension available;
- API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- required AddIn/API DLLs present;
- required Remoting DLLs present;
- .NET SDK x64 available under `C:\Program Files\dotnet` (SDK 8.0.427 confirmed);
- AddIn compiles against the real EPLAN 2.9.4 API assemblies;
- EPLAN loads the AddIn through `%APPDATA%\EPLAN\ShadowCopyAssemblies`;
- real lifecycle observed: `OnInit -> OnInitGui -> OnExit`;
- real `AudeuAiDispatchAction` construction and `IEplAction.OnRegister()` observed;
- action registered as `AudeuAiDispatch`, ordinal `20`;
- real `system.status` request/response spool round-trip PASS on 2026-10-26.

Gate A status:

- **Gate A1 — development environment:** PASS.
- **Gate A2 — AddIn runtime portion:** PASS for build, module load, action registration, action execution, request read, dispatch and response write.
- **Gate A2 — RemoteClient portion:** pending Task 6.

Gate B / M0 still requires the read-only project operations and LegacyHost/RemoteClient path.

## Current status

- [x] Repository/foundation documentation established.
- [x] Architecture 0.1 approved.
- [x] Sequential M0 -> M1 plan approved.
- [x] Gate A1 validated.
- [x] Task 1: repository scaffold and CI — branch `m0/task-1-scaffold`, draft PR #3.
- [x] Task 2: versioned bridge contracts — branch `m0/task-2-contracts`, issue #4 closed, draft PR #5.
- [x] Task 3: secure local spool — branch `m0/task-3-spool`, issue #6 closed, draft PR #7.
- [x] Task 4: minimal AddIn + fixed `AudeuAiDispatch` — branch `m0/task-4-dispatch`, Issue #8 completed, draft PR #11.
- [ ] Task 5: project session + read-only EPLAN operations.
- [ ] Task 6: LegacyHost RemoteClient gateway.
- [ ] Gate B / M0: prove complete read-only bridge on real EPLAN 2.9.
- [ ] Gate C / M1: prove safe Function Text Preview -> Apply -> Verify -> Undo/Rollback.

## Test runner ruling

xUnit v3 test projects are executable test applications. CI completion evidence must contain non-zero executed-test counts; a green step without an executed-test summary is insufficient.

## Task 1 — scaffold

Implemented solution/project shells, shared MSBuild configuration, central packages, EPLAN API path guards, MCP/Contracts shells and Windows CI.

Evidence: CI `39959463954` success.

## Task 2 — contracts

Implemented protocol `1.0`, `BridgeRequest` / `BridgeResponse`, session-scoped `ObjectReference`, stable error codes, and initial Change Plan contracts.

Evidence: CI `39975775380` success; Contracts **18/18**.

## Task 3 — secure spool

Implemented fixed root `%LOCALAPPDATA%\Audeu\EplanAiBridge`, GUID request/response names, request/response size limits, temp + same-directory rename, duplicate fail-closed behavior, response ID verification and stale cleanup.

Evidence:

- RED `39975854174`;
- implementation commit `08e386f9e690dc8dece51c96a20b659dd49c0512`;
- synchronized branch CI `39976839719` success;
- PR CI `39976847095` success;
- Contracts **18/18**;
- LegacyHost spool **8/8**.

## Task 4 — minimal AddIn and fixed dispatch action

Canonical tracking: Issue #8. Branch `m0/task-4-dispatch`. Draft stacked PR #11 remains unmerged.

Implemented:

- exactly one `EplanAddIn : IEplAddIn`;
- exactly one bridge `AudeuAiDispatchAction : IEplAction`;
- fixed action `AudeuAiDispatch`, ordinal `20`;
- only canonical GUID `REQUESTID` accepted;
- fixed local spool paths only;
- explicit operation whitelist;
- first operation `system.status`;
- stable protocol mismatch/unknown operation errors;
- valid request processing exceptions surface as `INTERNAL_ERROR` responses;
- diagnostic lifecycle/action logs for real-workstation validation;
- UTF-8 BOM-tolerant request reader to handle Windows PowerShell 5.1 generated requests;
- validation harness writes UTF-8 without BOM and reads responses explicitly as UTF-8.

### Task 4 important real-API findings

1. Real EPLAN 2.9.4 `ActionProperties` does not expose `Description`, despite an EPLAN documentation example using it. CI stub was corrected; `GetActionProperties(ref ActionProperties)` is a no-op.
2. `IEplAddIn.OnRegister()` is not expected to repeat for an already registered module. Real load proof is `OnInit/OnInitGui/OnExit` plus ShadowCopy location.
3. Windows PowerShell 5.1 `Set-Content -Encoding UTF8` adds a BOM. Direct `DataContractJsonSerializer.ReadObject()` on the raw stream rejected it. Regression coverage now proves BOM-prefixed requests are accepted.

### Task 4 TDD / CI evidence

Initial implementation:

- RED commit `7d4ea1a75ce4d57b845627744826b3c57ba0d34b`, CI `40017002588`;
- GREEN commit `ded33eec743cdb444fac79a2e9af9e35a276ee59`, CI `40017268214`;
- repo-side verification CI `40017826313`.

Real API compatibility regression:

- workstation CS1061 on `ActionProperties.Description`;
- RED commit `f1965572044651ca5e5fa78b10b7017916bdb586`, CI `40293424745`;
- GREEN commit `11f77768bf21e580b7d9ec284ab73647985e30db`, CI `40293650296`.

Runtime diagnostics:

- swallowed-exception RED CI `40297747094`;
- GREEN commit `27efa2eefcfa441872c6bb775d6b6e90efc6c4ca`, CI `40298100354`;
- action lifecycle instrumentation then confirmed real `Constructor` and `OnRegister:name=AudeuAiDispatch;ordinal=20`.

UTF-8 BOM regression:

- real workstation reached `Execute()` and returned `INTERNAL_ERROR SerializationException` on unexpected leading BOM character;
- RED commit `b401dbc5ecb39b2b5fe630c2c82bc32ff77de007`, CI `40303297164`: AddIn **12 passed / 1 failed**, BOM test was the single failure;
- GREEN implementation commits `3abadff8358f09d359a22b773aef3398ed720c6e` and `ebcc7ddf32565ea3c15acc815a2367de16250e24`;
- GREEN CI `40303614597`: Contracts **18/18**, LegacyHost spool **8/8**, AddIn **13/13**, MCP build and prerequisite guards success.

### Task 4 real-workstation acceptance — PASS

Real build:

```text
PASS: real-API AddIn build produced ...\Audeu.EplanAi.AddIn.dll
```

Real module/action registration evidence:

```text
OnInit
OnInitGui
OnExit
Constructor
OnRegister:entered
OnRegister:name=AudeuAiDispatch;ordinal=20
```

Final real `system.status` round-trip on 2026-10-26:

```text
Launching AudeuAiDispatch for request ea766505-2090-4c57-b931-4063bb03ae28
PASS: AudeuAiDispatch system.status round-trip succeeded for ea766505-2090-4c57-b931-4063bb03ae28
Execute:entered
Execute:request-id=ea766505-2090-4c57-b931-4063bb03ae28
Execute:success
```

This proves the real EPLAN path:

```text
EPLAN 2.9.4
 -> ShadowCopy AddIn load
 -> IEplAction discovery/registration
 -> AudeuAiDispatch Execute
 -> fixed request spool read
 -> whitelisted system.status dispatch
 -> fixed response spool write
 -> caller response verification
```

**Task 4 is complete. Issue #8 may be closed.**

## Immediate next actions

1. Do not merge PR #3, #5, #7, or #11 without explicit user approval.
2. Start Task 5 from the validated Task 4 head: project session identity and read-only EPLAN operations.
3. Task 5 target operations: active project, page list, selection and object reads; no writes.
4. Task 6 will then validate `EplanRemoteClient` and the LegacyHost -> EPLAN action path.
5. Do not start M1 write work until Gate B / M0 passes on real EPLAN 2.9.

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
