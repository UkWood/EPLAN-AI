# Task 9 Gate B Workstation Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repeatable official-SDK MCP workstation probe that proves the full read-only EPLAN bridge and the stale-session safety gate.

**Architecture:** A test-support .NET 8 console client uses `StdioClientTransport` to launch the Task 8 MCP server and invokes the five fixed tools while LegacyHost runs separately. A Windows PowerShell 5.1 wrapper builds/launches the probe; the probe pauses at the two UI-dependent close/reopen points and asserts `NO_ACTIVE_PROJECT` followed by `SESSION_STALE`.

**Tech Stack:** .NET 8, ModelContextProtocol 2.3.0 client API, System.Text.Json, Windows PowerShell 5.1, GitHub Actions.

**Spec:** `docs/design/task-9-gate-b-workstation-acceptance.md`

## Global Constraints

- Target workstation: EPLAN 2.9.4, API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`.
- Task 8 MCP server remains unchanged except fixes proven necessary by real acceptance.
- Probe has no EPLAN assembly reference and performs no writes.
- Use official MCP stdio client transport, not hand-written JSON-RPC.
- Gate B requires all functional checks and deterministic stale-session evidence.
- Task 10 / Gate C remains blocked until Gate B PASS.

## Review Focus

1. Stdio child process must launch the intended Release MCP executable, not `dotnet run` a stale source tree.
2. Tool results with `IsError=true` must be handled as MCP tool errors, not mistaken for transport failures.
3. Compact object reference numeric values must survive JSON parsing without narrowing or locale conversion.
4. Close/reopen must force session clear while no project is active before asserting stale reference.
5. Probe and wrapper must preserve exact Function Text including spaces/non-ASCII characters.

---

### Task 1: Official MCP probe scaffold and result helpers

**Files:**
- Create: `test-support/Audeu.EplanAi.McpProbe/Audeu.EplanAi.McpProbe.csproj`
- Create: `test-support/Audeu.EplanAi.McpProbe/Program.cs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes built `Audeu.EplanAi.Mcp.exe` path and expected project/function values.
- Produces console exit 0 only when Gate B sequence passes.

- [ ] Add a net8.0 console project referencing only `ModelContextProtocol`.
- [ ] Implement argument parsing for `--server`, `--expected-project-name`, `--expected-page-count`, `--expected-function-text`.
- [ ] Implement official `StdioClientTransport` + `McpClient.CreateAsync` composition.
- [ ] Implement helpers to call a tool, extract one `TextContentBlock`, parse JSON, and assert expected MCP error prefixes.
- [ ] Add a CI step that restores/builds the probe and verify build has no EPLAN references.
- [ ] Run full CI and require all existing non-zero test counts plus probe build PASS.

### Task 2: Gate B baseline read sequence

**Files:**
- Modify: `test-support/Audeu.EplanAi.McpProbe/Program.cs`

**Interfaces:**
- Uses official `McpClient.ListToolsAsync` / `CallToolAsync`.
- Produces saved in-memory old `projectSessionId` and compact Function reference for stale test.

- [ ] Require exactly the five approved tool names.
- [ ] Validate `eplan_status` ready/protocol 1.0.
- [ ] Validate active project name/page count/session ID.
- [ ] Validate page item count equals expected page count.
- [ ] Require exactly one selected Function and capture its compact reference.
- [ ] Invoke `eplan_get_object`, require `kind=Function`, and compare `functionText` exactly.
- [ ] Print concise PASS lines matching previous workstation probes.

### Task 3: Deterministic stale-session acceptance

**Files:**
- Modify: `test-support/Audeu.EplanAi.McpProbe/Program.cs`

**Interfaces:**
- Consumes old session/reference from Task 2.
- Produces Gate B PASS only after `NO_ACTIVE_PROJECT` and `SESSION_STALE` are both observed through MCP tool errors.

- [ ] Prompt user to close project and press Enter.
- [ ] Call old-session `eplan_list_pages`; require `IsError=true` and text prefix `NO_ACTIVE_PROJECT:`.
- [ ] Prompt user to reopen the same project and press Enter.
- [ ] Re-read active project and require expected name/page count plus a new non-empty session.
- [ ] Call `eplan_get_object` with old session/reference; require `SESSION_STALE:`.
- [ ] Dispose MCP client and print final Gate B PASS.

### Task 4: Workstation wrappers and durable instructions

**Files:**
- Create: `scripts/verify-task9-build.ps1`
- Create: `scripts/test-task9-mcp-readonly.ps1`
- Create: `docs/setup/task9-gate-b-validation.md`

**Interfaces:**
- `verify-task9-build.ps1` produces built LegacyHost, MCP server and probe.
- `test-task9-mcp-readonly.ps1` invokes the probe with safe defaults and exact Function Text.

- [ ] Build LegacyHost using real EPLAN 2.9.4 API path, then MCP and probe Release outputs.
- [ ] Prepend x64 dotnet path for this process if present; validate `dotnet --list-sdks` exposes an 8.0 SDK.
- [ ] Validate EPLAN process, MCP exe, probe exe and fixed LegacyHost setup.
- [ ] Prompt for expected Function Text only when not supplied.
- [ ] Document exact two-window workflow and acceptance output.
- [ ] Ensure every `.ps1` still parses under Windows PowerShell 5.1 in CI.

### Task 5: Gate B execution and state update

**Files:**
- Modify after workstation PASS: `docs/PROJECT_STATE.md`
- PR: `m0/task-9-gate-b` -> `m0/task-8-mcp-readonly` while Task 8 remains unmerged, or retarget to `main` after Task 8 merge.

- [ ] Run fresh branch CI and confirm probe build plus existing tests.
- [ ] On workstation run build verification, LegacyHost, and Task 9 probe.
- [ ] Record all six functional checks, Function Text check, `NO_ACTIVE_PROJECT`, `SESSION_STALE`, and clean shutdown evidence.
- [ ] Only after evidence, mark Gate B PASS and close Issue #22.
- [ ] Do not begin Task 10 until this gate is green.
