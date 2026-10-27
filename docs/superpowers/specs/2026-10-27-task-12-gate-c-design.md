# Task 12 / Gate C MCP Write Surface Design

## Intent

Complete M1 by exposing the already-approved Function Text workflow through MCP without weakening the closed-world boundary established by Tasks 10 and 11, then prove the full Preview -> Apply -> Verify -> Undo/Rollback flow on a disposable/safe EPLAN 2.9.4 project.

## Goals

- expose exactly three new MCP tools: `eplan_preview_set_function_text`, `eplan_apply_change_plan`, `eplan_undo_last_ai_change`;
- keep Change Plan payload immutable between preview and apply;
- ensure apply model input is only `changePlanId`;
- ensure undo model input is only the current `projectSessionId`;
- preserve stable bridge error codes as controlled MCP errors;
- keep all EPLAN API access outside the MCP process;
- provide repeatable Gate C workstation instructions and evidence.

## Non-goals / anti-goals

- no arbitrary EPLAN property IDs, action names, bridge operation names, paths or code;
- no generic set-property tool;
- no caller resubmission of target, old value or new value during apply;
- no persisted Change Plans across MCP restart in v0.1;
- no production-project mutation during Gate C;
- no expansion beyond Function `FUNC_TEXT`.

## Approaches considered

### A. Apply accepts the whole preview payload again
Rejected. This creates a second caller-controlled representation of target/old/new values and breaks the guarantee that apply uses exactly what was previewed.

### B. Apply accepts only `changePlanId` and MCP reconstructs the fixed bridge request from the stored immutable plan
Selected. The model cannot alter target/old/new after preview. The MCP store owns plan state; AddIn remains authoritative for the live EPLAN session, object identity, read-only state and old-value precondition.

### C. Move the Change Plan store into AddIn
Rejected for v0.1. It would duplicate Task 10 state ownership, couple planning to EPLAN runtime lifetime and expand the legacy boundary unnecessarily.

## Tool contracts

### `eplan_preview_set_function_text`

Model inputs:
- `projectSessionId`;
- `databaseIdentifier`;
- `typeIdentifier`;
- `objectIdentifier`;
- `stringIdentifier`;
- `newText`.

Behavior:
- construct a compact `ObjectReference`;
- call `FunctionTextChangePlanner.PreviewAsync`;
- planner performs only existing `object.get` / `Mode=read`;
- return compact plan JSON including `changePlanId`, status, target, old value and new value.

Metadata:
- `ReadOnly=true` because EPLAN is not mutated;
- `Destructive=false`;
- `Idempotent=false` because each successful preview creates a new plan identity;
- `OpenWorld=false`.

### `eplan_apply_change_plan`

Model input:
- `changePlanId` only.

Behavior:
1. reject empty plan ID;
2. load plan from `ChangePlanStore`;
3. atomically `ClaimForApply(planId, plan.ProjectSessionId)`;
4. require exactly one operation of kind `setFunctionText`;
5. copy immutable target/old/new into the fixed internal `change.apply` bridge request;
6. `Mode=write`, `ProjectSessionId=plan.ProjectSessionId`;
7. on success `MarkApplied` and return compact plan/status JSON;
8. on any bridge/transport/validation failure after claim, best-effort `MarkFailed`, then return the stable controlled error;
9. never release the claim for retry; a fresh preview is required after ambiguous failure.

Metadata:
- `ReadOnly=false`;
- `Destructive=true`;
- `Idempotent=false`;
- `OpenWorld=false`.

### `eplan_undo_last_ai_change`

Model input:
- `projectSessionId` only.

Behavior:
- send fixed `change.undoLast`, `Mode=write`, empty arguments;
- AddIn owns the reverse snapshot and live-session validation;
- return compact bridge result.

Metadata:
- `ReadOnly=false`;
- `Destructive=true`;
- `Idempotent=false`;
- `OpenWorld=false`.

## Error policy

Known stable errors are preserved as `McpException` codes, including `PLAN_NOT_FOUND`, `PLAN_ALREADY_APPLIED`, `PLAN_STALE`, `READ_ONLY_PROJECT`, session/object errors and transport/EPLAN failures. Unknown bridge errors map to `INTERNAL_ERROR`. A claimed plan is marked failed whenever apply does not reach a verified bridge success.

## Host composition

`Program.CreateBuilder` keeps existing singleton `ChangePlanStore` and `FunctionTextChangePlanner` and adds `.WithTools<EplanWriteTools>()` alongside `.WithTools<EplanReadTools>()`. No EPLAN assembly reference is added to MCP.

## Gate C acceptance

A dedicated workstation probe/script will use the official MCP stdio client against a disposable/safe EPLAN project and perform:

1. inspect selected Function and capture original displayed text;
2. preview a distinct temporary text;
3. immediately re-read and prove EPLAN still has the original text;
4. apply only by returned `changePlanId`;
5. re-read and prove the temporary text is visible through MCP and EPLAN;
6. undo using only the current session ID;
7. re-read and prove the exact original text is restored;
8. execute a controlled failure path before commit and prove the original value survives rollback;
9. stop probe and LegacyHost cleanly.

The acceptance script must refuse to run unless the user explicitly supplies the expected disposable project name (or another equally strong safe-project guard). Task 12 completion requires repo-side tests plus this real workstation evidence; code-only GREEN is not Gate C PASS.
