# Task 8 MCP Read-Only Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the .NET 8 stdio MCP facade for the five approved read-only EPLAN bridge operations.

**Architecture:** Keep MCP as a thin adapter over the fixed Task 7 named pipe. A testable `IBridgeClient` boundary separates transport from MCP tool mapping; production `NamedPipeBridgeClient` implements the exact Task 7 frame/JSON contract, while `EplanReadTools` creates only fixed read requests and surfaces compact JSON or controlled stable errors.

**Tech Stack:** .NET 8, `ModelContextProtocol` 2.3.0, `System.IO.Pipes`, `System.Text.Json`, xUnit v3, GitHub Actions Windows runner.

**Spec:** `docs/design/task-8-mcp-readonly-tools.md`

## Global Constraints

- MCP target: `net8.0`.
- MCP SDK: `ModelContextProtocol` 2.3.0.
- Pipe name: `Audeu.EplanAi.LegacyHost.v1` and may not come from MCP input.
- Protocol version: `1.0`.
- Pipe connect timeout: exactly 10 seconds.
- Request frame maximum: 1 MiB; response frame maximum: 16 MiB.
- MCP references no EPLAN API assembly.
- Expose exactly five read-only tools and no arbitrary bridge operation.
- Compact JSON results are returned unchanged from successful bridge `DataJson`.
- No HTTP/TCP listener and no write operation.

## Review Focus

1. CamelCase protocol drift between .NET 8 and Task 7 `DataContractJsonSerializer` must be caught by byte/frame tests.
2. A response with the wrong request ID or protocol version must never be accepted as the caller's result.
3. Cancellation/connect timeout must not leak a hanging MCP invocation; timeout maps deterministically to `OPERATION_TIMEOUT`.
4. Tool input cannot select a pipe, host, EPLAN action, bridge operation, filesystem path, or property ID.
5. SDK discovery must expose exactly five tools with read-only annotations and must not accidentally expose helper methods.

---

### Task 1: MCP pipe codec and transport boundary

**Files:**
- Create: `src/Audeu.EplanAi.Mcp/Transport/IBridgeClient.cs`
- Create: `src/Audeu.EplanAi.Mcp/Transport/BridgePipeProtocol.cs`
- Test: `tests/Audeu.EplanAi.Mcp.Tests/BridgePipeProtocolTests.cs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: `IBridgeClient.SendAsync(BridgeRequest, CancellationToken) -> Task<BridgeResponse>`.
- Produces internal codec methods that read/write Task 7-compatible request/response frames.

- [ ] **Step 1: Write RED codec tests** asserting the 4-byte little-endian prefix, canonical camelCase request fields, simple dictionary JSON, response decoding, 1 MiB/16 MiB bounds, and truncated frame rejection.
- [ ] **Step 2: Add MCP test execution to CI and run CI.** Expected: MCP test compile/run fails because transport/codec types do not exist.
- [ ] **Step 3: Implement the minimum `IBridgeClient` and `BridgePipeProtocol` needed by the tests.** Use `System.Text.Json` with explicit camelCase options and no EPLAN dependencies.
- [ ] **Step 4: Run MCP tests and full CI.** Expected: all green.
- [ ] **Step 5: Commit:** `feat: add MCP bridge pipe protocol`.

### Task 2: Fixed named-pipe client

**Files:**
- Create: `src/Audeu.EplanAi.Mcp/Transport/NamedPipeBridgeClient.cs`
- Test: `tests/Audeu.EplanAi.Mcp.Tests/NamedPipeBridgeClientTests.cs`

**Interfaces:**
- Consumes: `BridgePipeProtocol`, `BridgeRequest`, `BridgeResponse`.
- Produces: `NamedPipeBridgeClient : IBridgeClient` with production-fixed pipe name and 10-second connect timeout.

- [ ] **Step 1: Write RED tests** using an in-process named-pipe peer to prove request/response round-trip, request ID correlation, protocol-version validation, cancellation, and deterministic transport error mapping.
- [ ] **Step 2: Run MCP tests.** Expected: fail because `NamedPipeBridgeClient` is missing.
- [ ] **Step 3: Implement minimal client.** Connect to local fixed pipe only, send one request, read one response, close connection; expose no public pipe-name override.
- [ ] **Step 4: Run MCP tests and full CI.** Expected: all green.
- [ ] **Step 5: Commit:** `feat: add fixed MCP named-pipe client`.

### Task 3: Five read-only MCP tool mappings

**Files:**
- Create: `src/Audeu.EplanAi.Mcp/Tools/EplanReadTools.cs`
- Test: `tests/Audeu.EplanAi.Mcp.Tests/EplanReadToolsTests.cs`

**Interfaces:**
- Consumes: `IBridgeClient` through dependency injection.
- Produces exact MCP tools `eplan_status`, `eplan_get_active_project`, `eplan_list_pages`, `eplan_get_selection`, `eplan_get_object`.

- [ ] **Step 1: Write RED mapping tests** with a recording fake `IBridgeClient`: assert fixed bridge operation, fresh request ID, protocol `1.0`, mode `read`, session forwarding, invariant object-reference arguments, successful compact JSON passthrough, and bridge error-code preservation.
- [ ] **Step 2: Write RED reflection/SDK metadata test** asserting exactly five `[McpServerTool]` methods and `ReadOnly=true` on each.
- [ ] **Step 3: Run MCP tests.** Expected: fail because the tool type is missing.
- [ ] **Step 4: Implement only the five tool methods.** Validate Guid/reference inputs before transport; throw controlled `McpException` messages beginning with the stable bridge code for errors.
- [ ] **Step 5: Run MCP tests and full CI.** Expected: all green.
- [ ] **Step 6: Commit:** `feat: expose read-only EPLAN MCP tools`.

### Task 4: stdio MCP host composition

**Files:**
- Modify: `src/Audeu.EplanAi.Mcp/Program.cs`
- Modify if required: `src/Audeu.EplanAi.Mcp/Audeu.EplanAi.Mcp.csproj`
- Test: `tests/Audeu.EplanAi.Mcp.Tests/McpHostCompositionTests.cs`

**Interfaces:**
- Consumes: `NamedPipeBridgeClient`, `EplanReadTools`, ModelContextProtocol 2.3.0.
- Produces: executable stdio MCP server; logging never writes ordinary output to stdout.

- [ ] **Step 1: Write RED composition test** proving the host/service collection can resolve `IBridgeClient` and register the tool type without any EPLAN assembly reference.
- [ ] **Step 2: Run MCP tests.** Expected: fail because `Program` is still the empty shell.
- [ ] **Step 3: Implement host composition** with `Host.CreateApplicationBuilder`, stderr logging, `AddMcpServer().WithStdioServerTransport().WithTools<EplanReadTools>()`, and DI registration for `IBridgeClient`.
- [ ] **Step 4: Run MCP tests and full CI.** Expected: all green.
- [ ] **Step 5: Commit:** `feat: host EPLAN MCP server over stdio`.

### Task 5: Task 8 completion evidence

**Files:**
- Modify: `docs/PROJECT_STATE.md`
- PR: `m0/task-8-mcp-readonly` -> `main`

**Interfaces:**
- Produces durable Task 8 status and evidence for Task 9.

- [ ] **Step 1: Run fresh full CI on final branch head.** Record run ID and counts/status.
- [ ] **Step 2: Inspect project references** and record that MCP has no EPLAN assembly dependency.
- [ ] **Step 3: Update `docs/PROJECT_STATE.md`** with RED/GREEN evidence, final head, CI, five tool names, and explicit note that real MCP-to-EPLAN acceptance remains Task 9.
- [ ] **Step 4: Open a PR linked to issue #20.** Keep scope read-only; no Task 9 or write behavior in this PR.
- [ ] **Step 5: Review diff and run final PR CI before merge.**