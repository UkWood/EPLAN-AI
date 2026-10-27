# Task 10 MCP Change Plan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the MCP-side immutable `setFunctionText` Change Plan preview/store lifecycle without introducing any EPLAN write behavior.

**Architecture:** Strengthen the shared `ChangeOperation` payload so target identity is defensively snapshotted, then add an MCP singleton `ChangePlanStore` with an atomic apply claim. Add `FunctionTextChangePlanner` that reads the current Function through existing `object.get`, constructs/stores a previewed plan, and never emits a write request.

**Tech Stack:** C# / .NET Standard 2.0 contracts, .NET 8 MCP server, xUnit v3, existing `IBridgeClient` transport.

**Spec:** `docs/superpowers/specs/2026-10-27-task-10-change-plan-design.md`

## Global Constraints

- Parent protocol remains version `1.0`.
- MCP process references no EPLAN assembly.
- First and only supported plan kind is `setFunctionText`.
- Preview may call only existing read operation `object.get` with `Mode = "read"`.
- Initial Function Text limit is 1024 UTF-16 code units, inclusive.
- Plans are in-memory only and disappear on MCP process restart.
- A plan is bound to one `projectSessionId`.
- Plan payload cannot be modified after creation.
- A plan may be claimed for apply at most once.
- No AddIn/LegacyHost write code and no new MCP write tool in Task 10.

## Review Focus

- caller mutates an `ObjectReference` after preview -> stored plan target must remain unchanged;
- two concurrent apply claims -> exactly one succeeds, the other receives `PLAN_ALREADY_APPLIED`;
- current session differs from stored session -> `PLAN_STALE`, never a plan for the new project;
- 1025 UTF-16 code units -> validation fails before any bridge request;
- malformed/unknown bridge response -> controlled `INTERNAL_ERROR`, never an accidental plan.

---

### Task 1: Make Change Plan payload deeply immutable

**Files:**
- Modify: `src/Audeu.EplanAi.Contracts/ChangeOperation.cs`
- Modify: `src/Audeu.EplanAi.Contracts/BridgeErrorCodes.cs`
- Test: `tests/Audeu.EplanAi.Contracts.Tests/ChangePlanTests.cs`

**Interfaces:**
- Consumes: existing `ObjectReference`, `ChangePlan`, `ChangeOperation` contracts.
- Produces: `ChangeOperation.Target` defensive-copy semantics; `BridgeErrorCodes.PlanNotFound`; `BridgeErrorCodes.PlanStale`.

- [ ] **Step 1: Write failing contract tests**
  - `ChangeOperation_SnapshotsTargetAtConstruction`
  - `ChangeOperation_TargetGetterReturnsDefensiveCopy`
  - extend stable error-code test for `PLAN_NOT_FOUND` and `PLAN_STALE`.

- [ ] **Step 2: Run Contracts tests and verify RED**
  - Expected: target snapshot tests fail on current reference-retaining implementation and new error constants do not compile.

- [ ] **Step 3: Implement minimal defensive target snapshot and constants**
  - `ChangeOperation` stores target identity fields privately and reconstructs a new `ObjectReference` in the getter.
  - add the two approved error constants.

- [ ] **Step 4: Run full Contracts test suite**
  - Expected: all Contracts tests pass with non-zero count.

- [ ] **Step 5: Commit**
  - `feat: make change plan payload immutable`

---

### Task 2: Add thread-safe in-memory Change Plan store

**Files:**
- Create: `src/Audeu.EplanAi.Mcp/ChangePlans/ChangePlanException.cs`
- Create: `src/Audeu.EplanAi.Mcp/ChangePlans/ChangePlanStore.cs`
- Create: `tests/Audeu.EplanAi.Mcp.Tests/ChangePlanStoreTests.cs`

**Interfaces:**
- Consumes: immutable `ChangePlan` / `ChangePlanStatuses` and stable plan error constants from Task 1.
- Produces: `Add`, `Get`, `GetForSession`, `ClaimForApply`, `MarkApplied`, `MarkFailed`.

- [ ] **Step 1: Write failing store tests**
  - add/get returns same plan;
  - missing plan -> `PLAN_NOT_FOUND`;
  - session mismatch -> `PLAN_STALE`;
  - first claim succeeds and second claim -> `PLAN_ALREADY_APPLIED`;
  - failed/applied plan is terminal;
  - 16 concurrent claims produce exactly one success and 15 `PLAN_ALREADY_APPLIED` failures.

- [ ] **Step 2: Run MCP tests and verify RED**
  - Expected: missing `Audeu.EplanAi.Mcp.ChangePlans` types.

- [ ] **Step 3: Implement minimal synchronized in-memory store**
  - one private lock protects dictionary and claimed-plan set;
  - no disk persistence or cleanup timer.

- [ ] **Step 4: Run MCP suite**
  - Expected: store tests and all existing MCP tests pass.

- [ ] **Step 5: Commit**
  - `feat: add in-memory EPLAN change plan store`

---

### Task 3: Add read-only Function Text planner

**Files:**
- Create: `src/Audeu.EplanAi.Mcp/ChangePlans/FunctionTextChangePlanner.cs`
- Create: `tests/Audeu.EplanAi.Mcp.Tests/FunctionTextChangePlannerTests.cs`

**Interfaces:**
- Consumes: `IBridgeClient.SendAsync(BridgeRequest, CancellationToken)`, `ChangePlanStore.Add(ChangePlan)`.
- Produces: `Task<ChangePlan> PreviewAsync(ObjectReference target, string newText, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write failing planner tests**
  - emits exactly one `object.get` / `read` request with exact compact target arguments;
  - stores old displayed Function Text and proposed new text in one `setFunctionText` operation;
  - 1024 UTF-16 units accepted;
  - 1025 rejected with `VALIDATION_FAILED` before bridge call;
  - null text rejected before bridge call;
  - known `SESSION_STALE` bridge failure preserved;
  - unknown bridge error maps to `INTERNAL_ERROR`;
  - malformed JSON / non-Function payload maps to controlled failure;
  - source/returned target mutations do not change stored target.

- [ ] **Step 2: Run MCP tests and verify RED**
  - Expected: missing `FunctionTextChangePlanner`.

- [ ] **Step 3: Implement minimal planner**
  - use `TimeProvider` for creation UTC;
  - parse only `kind` and `functionText` from compact JSON;
  - normalize JSON null Function Text to empty string;
  - never issue any operation other than `object.get`.

- [ ] **Step 4: Run MCP suite**
  - Expected: all planner/store/existing MCP tests pass.

- [ ] **Step 5: Commit**
  - `feat: preview Function Text change plans`

---

### Task 4: Compose Task 10 services and verify branch

**Files:**
- Modify: `src/Audeu.EplanAi.Mcp/Program.cs`
- Modify: `tests/Audeu.EplanAi.Mcp.Tests/McpHostCompositionTests.cs`
- Modify: `docs/PROJECT_STATE.md`

**Interfaces:**
- Consumes: `ChangePlanStore`, `FunctionTextChangePlanner`.
- Produces: singleton DI composition for future Task 12 tool exposure while keeping exactly the existing five MCP tools.

- [ ] **Step 1: Write failing DI tests**
  - `ChangePlanStore` resolves as singleton;
  - `FunctionTextChangePlanner` resolves and shares the singleton store;
  - existing `IBridgeClient` remains registered.

- [ ] **Step 2: Run MCP tests and verify RED**
  - Expected: new services are not registered.

- [ ] **Step 3: Register `TimeProvider.System`, store and planner**
  - Do not add a new MCP tool registration.

- [ ] **Step 4: Run full repository CI-equivalent test/build suite**
  - Contracts, LegacyHost, AddIn, MCP, Task 9 probe tests; prerequisite guards; PowerShell validation.
  - Expected: all green with non-zero executed test counts.

- [ ] **Step 5: Update `PROJECT_STATE.md`**
  - Record Task 10 repo-side implementation state and explicitly state that no EPLAN mutation exists yet.

- [ ] **Step 6: Commit**
  - `feat: compose MCP change plan lifecycle`

## Execution ruling

The user explicitly requested to continue according to the already approved implementation plan. Execution therefore proceeds inline on isolated branch `m1/task-10-change-plans` without an additional approval pause. No merge to `main` is authorized by that instruction; merging remains a separate explicit user decision.