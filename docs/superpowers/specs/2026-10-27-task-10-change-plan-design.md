# Task 10 MCP Change Plan Design

**Date:** 2026-10-27  
**Status:** approved execution of the existing EPLAN AI Bridge 0.1 Task 10 plan  
**Parent spec:** `docs/design/eplan-ai-bridge-v0.1.md`  
**Issue:** #24

## Intent

Build the MCP-side in-memory Change Plan lifecycle required before any EPLAN write path exists. The first and only supported plan kind in 0.1 is `setFunctionText` for one current-session `Function` reference.

A preview must capture the exact current displayed Function Text from EPLAN through the existing read-only `object.get` bridge operation, store the exact proposed new text, and produce an immutable plan payload. Task 10 must not call `change.apply`, modify EPLAN, or expose a new mutating MCP tool.

## Goals

- immutable plan payload: plan ID, creation time, session, target, old value, new value and operation kind cannot be altered after preview;
- in-memory MCP-process-only plan store;
- exact binding to the current project session;
- atomic single claim for application so concurrent callers cannot both apply the same plan;
- terminal lifecycle `previewed -> applied|failed`;
- stable `PLAN_NOT_FOUND`, `PLAN_ALREADY_APPLIED` and `PLAN_STALE` failures;
- Function Text input limit of 1024 UTF-16 code units, inclusive;
- preview uses only `object.get` with bridge request mode `read`.

## Non-goals

- no AddIn write implementation;
- no `change.apply` or `change.undoLast` invocation;
- no EPLAN Transaction/SafetyPoint/Undo work;
- no persistence of plans across MCP restart;
- no arbitrary property editing;
- no new MCP write tool in Task 10.

## Approaches considered

### A. Store only; defer preview capture to Task 12

Smallest code change, but it leaves Task 10 unable to prove that a plan stores the actual EPLAN old value and violates the approved rule that preview may read EPLAN. Rejected.

### B. MCP-side planner plus in-memory store — chosen

`FunctionTextChangePlanner` performs one existing read-only `object.get`, validates the returned Function snapshot, and creates a `ChangePlan`. `ChangePlanStore` owns lookup, session checks, atomic apply claiming and terminal state transitions. This keeps lifecycle/policy in the MCP process exactly as the parent architecture specifies and gives Task 11 a safe internal API to consume.

### C. Create/store plans inside the EPLAN AddIn

Would bind policy state to the EPLAN process and expand the EPLAN-side surface before write safety is designed. It also conflicts with the approved process-boundary rule that MCP owns change-plan lifecycle and confirmation state. Rejected.

## Design

### Immutable payload

`ChangeOperation` will stop retaining a caller-owned mutable `ObjectReference`. It snapshots the five reference identity fields in its constructor and returns a fresh `ObjectReference` from `Target`. Strings are immutable and remain get-only. `ChangePlan` continues to copy the operation list into a read-only list.

Status is lifecycle state, not payload: it remains the only intentionally mutable part of a `ChangePlan`.

### `ChangePlanStore`

Lives under `Audeu.EplanAi.Mcp.ChangePlans` and is registered as a singleton.

The store keeps plans only in memory and synchronizes all lifecycle operations with one private lock. It also keeps a private set of plan IDs already claimed for apply.

Public behavior:

- `Add(ChangePlan plan)` stores a new previewed plan;
- `Get(Guid planId)` returns an existing plan or `PLAN_NOT_FOUND`;
- `GetForSession(Guid planId, Guid currentProjectSessionId)` additionally rejects a mismatched session with `PLAN_STALE`;
- `ClaimForApply(Guid planId, Guid currentProjectSessionId)` atomically validates existence/session/previewed state and permanently claims the plan; a second claim returns `PLAN_ALREADY_APPLIED` even before Task 11 finishes the first application;
- `MarkApplied(Guid planId)` transitions a claimed previewed plan to `applied`;
- `MarkFailed(Guid planId)` transitions a claimed previewed plan to `failed`.

A failed plan is terminal and cannot be reclaimed.

### `FunctionTextChangePlanner`

Dependencies: `IBridgeClient`, `ChangePlanStore`, `TimeProvider`.

`PreviewAsync(ObjectReference target, string newText, CancellationToken cancellationToken)`:

1. validates target/session/string identifier and non-null new text;
2. rejects `newText.Length > 1024` (`string.Length` is the required UTF-16 code-unit count in .NET);
3. sends exactly one `BridgeRequest` with operation `object.get`, mode `read`, current target session, and the four compact identity arguments;
4. propagates known stable bridge failures through `ChangePlanException`; unknown failures become `INTERNAL_ERROR`;
5. parses compact `DataJson`, requires `kind == "Function"`, and captures displayed `functionText` (JSON null is normalized to empty string);
6. creates one `setFunctionText` `ChangeOperation`, a new `ChangePlan` with a new GUID and `TimeProvider.GetUtcNow()`, stores it, and returns it.

No write bridge operation is reachable from this class.

### Errors

Add protocol constants already approved by the parent design:

- `PLAN_NOT_FOUND`;
- `PLAN_STALE`.

`PLAN_ALREADY_APPLIED` already exists. `ChangePlanException` carries a stable `ErrorCode` plus safe message and contains no stack-trace serialization behavior.

### Dependency injection

`Program.CreateBuilder` registers:

- singleton `TimeProvider.System`;
- singleton `ChangePlanStore`;
- singleton `FunctionTextChangePlanner`.

No MCP tool is added in Task 10, so the externally discoverable tool count remains exactly five.

## Test strategy

1. Contract regression: mutating the source/returned `ObjectReference` cannot alter the target stored in a `ChangeOperation`/`ChangePlan`.
2. Store tests: add/get, missing plan, stale session, atomic single claim, applied/failed terminal state, concurrent claims yield exactly one success.
3. Planner tests: exact read-only `object.get` request, old/new capture, 1024 accepted, 1025 rejected before bridge call, null rejected, stale/object errors preserved, unknown error mapped to `INTERNAL_ERROR`, malformed/non-Function JSON rejected safely.
4. Host composition test: store/planner are singleton services and existing five-tool MCP registration remains intact.
5. Full CI must remain green with non-zero MCP test discovery.

## Safety invariant for Task 10

A Task 10 test must be able to assert that every bridge request emitted by preview has `Mode == "read"` and `Operation == "object.get"`. Any write operation in this branch is a design violation.