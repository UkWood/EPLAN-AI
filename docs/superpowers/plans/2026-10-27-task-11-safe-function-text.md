# Task 11 Safe Function Text Apply Implementation Plan

> **Execution:** follow TDD task-by-task. This branch is stacked on `m1/task-10-change-plans`; do not merge without explicit user approval.

**Goal:** Implement the internal `change.apply` / `change.undoLast` Function Text write path inside the AddIn/EPLAN boundary with exact session/identity/precondition validation, transactional rollback, post-write verification and duplicate-plan protection. Do not expose model-callable write MCP tools yet.

**Spec:** `docs/superpowers/specs/2026-10-27-task-11-safe-function-text-design.md`

## Task 1 — Write protocol/service boundary

**Files**
- Modify `src/Audeu.EplanAi.Contracts/BridgeErrorCodes.cs`
- Create `src/Audeu.EplanAi.AddIn/WriteModel/*` domain/interface files
- Create `tests/Audeu.EplanAi.AddIn.IntegrationTests/WriteOperationServiceTests.cs`

**RED tests**
- `READ_ONLY_PROJECT` stable code;
- `change.apply` requires mode `write`, current session and exact fixed args;
- malformed/stale/read-only failures occur before adapter write;
- duplicate `changePlanId` calls adapter exactly once and second returns `PLAN_ALREADY_APPLIED`;
- apply success stores last-change snapshot;
- undo requires current session/last state, reverses exact old/new values and clears only on success.

**GREEN implementation**
- `IEplanWriteAdapter`, request/result snapshots;
- `WriteOperationService` with permanent per-process claimed-plan-ID set and one last-change slot;
- no EPLAN API calls in service policy.

## Task 2 — Share session tracker and register fixed internal operations

**Files**
- Modify `src/Audeu.EplanAi.AddIn/Dispatch/OperationRegistry.cs`
- Modify AddIn registry tests.

**RED tests**
- registry exposes existing status/read operations plus only `change.apply` and `change.undoLast`;
- unknown write names stay rejected;
- session established through `project.getActive` is accepted by injected write service using the same tracker.

**GREEN implementation**
- default registry composes one `ProjectSessionTracker` shared by `ReadOperationService` and `WriteOperationService`;
- fixed operations only.

## Task 3 — Real EPLAN write adapter transaction boundary

**Files**
- Create `src/Audeu.EplanAi.AddIn/WriteModel/EplanWriteAdapter.cs`
- Create `src/Audeu.EplanAi.AddIn/WriteModel/IWriteExecutionHook.cs`
- Extend `test-support/EplanApiStubs.cs`
- Add adapter integration tests.

**RED tests**
- exact reference validation and Function-only guard;
- previewed old value mismatch -> `PLAN_STALE`, no mutation;
- assignment changes only FUNC_TEXT and uses language-independent MultiLangString;
- injected failure immediately after assignment restores old value;
- post-read mismatch restores old value;
- implicit EPLAN transaction commit is failure;
- verified success commits Transaction then SafetyPoint and wraps change in UndoStep.

**GREEN implementation sequence**
1. resolve current Function and validate identity/read-only;
2. re-read displayed current text and compare exact old value;
3. create `UndoStep`;
4. create `SafetyPoint`;
5. create `Transaction`;
6. `MultiLangString.SetAsString(newValue)` and assign fixed `FUNC_TEXT`;
7. invoke internal hook;
8. read displayed text back;
9. reject implicit internal transaction;
10. commit transaction;
11. commit safety point;
12. return success.

## Task 4 — End-to-end AddIn dispatch tests and final verification

**Files**
- Add/extend dispatch integration tests;
- update `docs/PROJECT_STATE.md`.

**Tests**
- `AudeuAiDispatch` fixed operation handling returns stable write errors and never forwards operation name as EPLAN action;
- no external MCP tool count change (MCP suite still discovers only five read-only tools);
- full repository CI with non-zero test counts;
- build against stubs only for repo-side Task 11; real mutation is deferred to Task 12 Gate C.

## Safety rulings

- Never accept arbitrary property IDs or property names.
- Never accept arbitrary action names.
- No write request can use `Mode=read` or omit the current session.
- Old-value mismatch is `PLAN_STALE`, never overwrite.
- A `changePlanId` is claimed before EPLAN mutation; no retry of the same ID.
- If atomicity is uncertain because an implicit EPLAN transaction occurred, report failure rather than success.
- No real workstation mutation during Task 11 repo-side completion.