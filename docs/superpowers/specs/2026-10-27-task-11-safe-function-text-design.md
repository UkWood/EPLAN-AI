# Task 11 Safe Function Text Apply Design

**Date:** 2026-10-27  
**Status:** execution design for approved EPLAN AI Bridge 0.1 Task 11  
**Parent spec:** `docs/design/eplan-ai-bridge-v0.1.md`  
**Issue:** #26  
**Stacked on:** Task 10 / PR #25

## Intent

Implement the first and only EPLAN mutation allowed in release 0.1: applying a previously previewed `setFunctionText` change plan to one current-session Function, with strong precondition checks, transaction/safety rollback, post-write verification, EPLAN undo protocol, duplicate-plan protection and a session-bound internal undo-last path.

Task 11 adds **internal bridge operations only**. It does not expose a new MCP tool. Task 12 remains responsible for model-callable preview/apply/undo tools and real Gate C workstation mutation acceptance.

## EPLAN 2.9 API rulings

The implementation follows the verified EPLAN 2.9 API behavior:

- `MultiLangString.SetAsString(string)` stores plain text as language-independent `??_??@...`, matching the language-independent Function Text representation observed on the real workstation.
- `SafetyPoint.Create()` records changes; uncommitted disposal/rollback reverses them; `Commit()` accepts changes and releases locks.
- `TransactionManager.CreateTransaction()` supplies the API transaction; `Transaction.IsImplicitEplanTransactionCommited` is checked before claiming atomic success. If EPLAN opened and completed an internal transaction during the API transaction, the operation fails closed instead of reporting success.
- `UndoManager.CreateUndoStep()` wraps persistent Function changes so EPLAN's undo protocol is preserved.

## Closed-world bridge surface

Task 11 registers only the already-approved fixed internal operation names:

- `change.apply`
- `change.undoLast`

They are handled by the existing fixed `AudeuAiDispatch` action. No bridge operation is ever forwarded as an EPLAN action name.

### `change.apply`

Required request:

- `Mode == "write"`;
- `ProjectSessionId` is present and current;
- arguments contain exactly the fixed plan payload fields:
  - `changePlanId` canonical GUID;
  - `databaseIdentifier`;
  - `typeIdentifier`;
  - `objectIdentifier`;
  - `stringIdentifier`;
  - `oldValue`;
  - `newValue`.

The property being changed is not supplied by the caller. It is always `FUNC_TEXT`.

### `change.undoLast`

Required request:

- `Mode == "write"`;
- `ProjectSessionId` is present and current;
- no arbitrary target/property/new text arguments.

The AddIn retains one minimal last-successful-AI-change snapshot in memory: session, exact Function reference, old value and applied new value. Undo is implemented as a new guarded reverse Function Text write (`expected current == applied new`, replacement = old). This avoids keeping an `UndoStep` object alive across dispatch calls; EPLAN documents UndoStep as disposable and used within a bounded scope.

After successful undo, the internal last-change slot is cleared. Closing/reopening or switching project invalidates it through session validation.

## Shared AddIn session tracker

Read and write services must share one long-lived `ProjectSessionTracker` instance inside `OperationRegistry`.

This is mandatory: preview/read operations establish the session GUID returned to MCP, and `change.apply`/`change.undoLast` must validate against exactly that same tracker. A second independent tracker would make valid plans look stale or, worse, establish an unrelated session.

## Duplicate apply defense

Task 10 already atomically claims a Change Plan in MCP. Task 11 adds defense-in-depth inside the AddIn using `changePlanId`:

- before entering EPLAN mutation, the write service atomically claims the plan ID in an in-memory set;
- a repeated `change.apply` with the same ID returns `PLAN_ALREADY_APPLIED` and never reaches the adapter;
- the claim is intentionally permanent for the lifetime of the AddIn action service, even if the attempt fails safely. A previewed plan is single-use; retrying an ambiguous write is more dangerous than requiring a fresh preview.

This also makes a lost response after a successful commit fail closed on retry instead of applying twice.

## Write service policy

`WriteOperationService` owns bridge/request policy and current-session state. It depends on:

- `IEplanWriteAdapter`;
- the shared `ProjectSessionTracker`.

For `change.apply` it:

1. requires mode `write`;
2. obtains current project through the adapter;
3. clears sessions/last-change if no project;
4. requires current session match;
5. rejects a read-only project with `READ_ONLY_PROJECT`;
6. parses the exact fixed Function reference + plan ID + old/new values;
7. atomically claims `changePlanId`;
8. calls adapter apply with exact identity and old/new values;
9. on success stores the one last-change snapshot and returns compact applied result;
10. maps controlled adapter failures to stable bridge errors without stack traces.

`change.undoLast` requires a current matching project session and a stored last change for that exact session; it performs guarded reverse apply and clears the slot only after success.

## EPLAN write adapter

`EplanWriteAdapter` is the only new class that writes EPLAN DataModel state.

### Target resolution

Before any mutation:

1. active project exists and matches `databaseIdentifier`;
2. `StorableObject.FromStringIdentifier` resolves successfully;
3. object is valid and belongs to the same database;
4. type/object identifiers exactly match;
5. object is a `Function`;
6. Function/Project is not read-only;
7. displayed Function Text is re-read with the same `Translate.GetStringToDisplayInDialogs(project, value.ToMultiLangString())` boundary used by read operations;
8. displayed current text exactly equals previewed `oldValue`.

A mismatch between previewed old value and current displayed value returns `PLAN_STALE`; it never overwrites a human/EPLAN change that happened after preview.

### Atomic apply sequence

For one guarded Function Text change:

```text
resolve + validate Function
  -> read displayed current value == expected old
  -> using UndoManager / UndoStep
  -> using SafetyPoint.Create()
  -> using TransactionManager / Transaction
  -> create MultiLangString; SetAsString(newValue)
  -> assign fixed FUNC_TEXT only
  -> re-read displayed Function Text
  -> require exact newValue
  -> require !transaction.IsImplicitEplanTransactionCommited
  -> transaction.Commit()
  -> safetyPoint.Commit()
  -> return success
```

No commit occurs before the post-condition and implicit-transaction checks pass.

On exception/verification failure before commit, the transaction and uncommitted SafetyPoint are disposed; SafetyPoint rollback is the final safety net. The adapter returns a controlled failure, never a success with uncertain state.

## Failure injection seam

Tests need to prove rollback after assignment but before commit. `EplanWriteAdapter` therefore receives an internal/test-only `IWriteExecutionHook` (default no-op) invoked immediately after assignment and before read-back/commit. A test hook may throw to exercise the failure path. No hook is reachable from bridge/MCP input.

## Function Text representation

A new value is built with:

```csharp
using (var text = new MultiLangString())
{
    text.SetAsString(newValue);
    function.Properties.FUNC_TEXT = text;
}
```

The caller never supplies EPLAN's internal `??_??@` serialization as a special mode. It supplies displayed plain text; `SetAsString` creates the language-independent representation.

## Undo strategy

Task 11 uses UndoStep around both forward and reverse persistent changes so the normal EPLAN undo protocol is not silently cleared.

The bridge-level `change.undoLast` is deterministic rather than relying on a disposed historical `UndoStep`: it re-resolves the Function, requires current displayed text to equal the last applied new value, then writes the stored old value through the same guarded atomic pipeline. This makes undo session-bound, testable and safe across separate `AudeuAiDispatch` calls.

## Stable errors

Add the approved `READ_ONLY_PROJECT` constant.

Mapping:

- no active project -> `NO_ACTIVE_PROJECT`;
- request/current session mismatch -> `SESSION_STALE`;
- stored last-change session mismatch -> `PLAN_STALE`;
- duplicate apply ID -> `PLAN_ALREADY_APPLIED`;
- malformed request/missing undo -> `VALIDATION_FAILED`;
- unresolved target -> `OBJECT_NOT_FOUND`;
- identity mismatch -> `OBJECT_INVALID`;
- non-Function -> `OBJECT_TYPE_NOT_ALLOWED`;
- read-only project/function -> `READ_ONLY_PROJECT`;
- previewed old text no longer matches current text -> `PLAN_STALE`;
- post-condition failure / implicit internal EPLAN transaction / unexpected EPLAN write error -> `EPLAN_ACTION_FAILED`;
- unexpected implementation error at dispatcher boundary remains `INTERNAL_ERROR`.

## Test strategy

### Service tests without EPLAN

- only fixed `change.apply` and `change.undoLast` are registered in addition to existing reads;
- read and write services share a session tracker;
- mode must be `write`;
- malformed/cross-session/read-only requests fail before adapter mutation;
- duplicate plan ID reaches adapter exactly once;
- successful apply stores exact last-change state;
- undo only works in current session, calls reverse apply with exact expected/new values, then clears state;
- failed undo retains state for diagnosis/fresh decision.

### EPLAN adapter tests with API stubs

- exact identity validation;
- non-Function rejection;
- old-value precondition prevents mutation;
- only `FUNC_TEXT` is changed;
- plain text is written as language-independent MultiLangString;
- successful write is read back and verified before commits;
- injected failure after assignment rolls back to original value;
- post-condition mismatch rolls back;
- implicit internal EPLAN transaction commit is detected and reported as failure;
- success commits transaction then SafetyPoint;
- UndoStep is created/disposed around persistent change.

## Task boundary

Task 11 is repo-side/internal bridge implementation only. Do **not** run a real workstation mutation as part of Task 11 completion. Task 12 will add the MCP tools and the explicit disposable-project Gate C acceptance sequence: preview -> prove unchanged -> apply -> verify -> undo -> verify original -> injected failure -> verify rollback.