# Task 7 Current-User Named-Pipe LegacyHost Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fixed current-user-only named-pipe server to LegacyHost that serializes MCP-side bridge requests onto the already-proven persistent EPLAN RemoteGateway.

**Architecture:** LegacyHost owns one `RemoteGateway` for its process lifetime. A single secured `NamedPipeServerStream` accept/process loop reads one length-prefixed UTF-8 `BridgeRequest`, executes it, writes one framed `BridgeResponse`, then accepts the next client; this naturally limits EPLAN work to one request at a time.

**Tech Stack:** .NET Framework 4.7.2, `System.IO.Pipes`, `PipeSecurity`, current Windows SID, `DataContractJsonSerializer`, existing `BridgeRequest/BridgeResponse`, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-10-26-task-7-named-pipe-design.md`

## Global Constraints

- Pipe name is exactly `Audeu.EplanAi.LegacyHost.v1`.
- Current Windows user SID is the only allowed principal.
- Request payload maximum is exactly 1 MiB.
- No TCP/HTTP listener.
- No caller-controlled pipe/path/action/host/port.
- One EPLAN request in flight at a time.
- LegacyHost owns and gracefully disconnects RemoteClient.

## Review Focus

1. Oversized length prefix must fail before payload allocation or EPLAN execution.
2. Partial stream reads must reconstruct frames correctly.
3. A second client must not execute while the first request is in flight.
4. Pipe ACL must not accidentally inherit broad Everyone/Users access.
5. Cancellation/shutdown must disconnect the persistent RemoteGateway exactly once and stop accepting clients.

---

### Task 1: Pipe protocol and size boundary

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Pipe/PipeProtocol.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/PipeProtocolTests.cs`

**Interfaces:**
- Produces fixed `PipeName`, `MaxRequestBytes`, `ReadRequestAsync`, `WriteResponseAsync`.

- [ ] Write failing tests for exact constants, framed round-trip, partial reads, zero/negative/oversized length rejection and malformed JSON.
- [ ] Run LegacyHost tests and confirm RED.
- [ ] Implement minimal framing + `DataContractJsonSerializer`.
- [ ] Run tests and confirm GREEN.

### Task 2: Current-user pipe ACL

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Pipe/CurrentUserPipeSecurity.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/CurrentUserPipeSecurityTests.cs`

**Interfaces:**
- Produces `PipeSecurity Create(SecurityIdentifier currentUserSid)` and `SecurityIdentifier GetCurrentUserSid()`.

- [ ] Write failing tests requiring owner=current SID and allow rules only for current SID.
- [ ] Confirm RED.
- [ ] Implement explicit protected ACL with current-user full control/read-write-create-instance rights needed by the server/client.
- [ ] Confirm GREEN.

### Task 3: Serialized pipe server

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Pipe/IBridgeRequestExecutor.cs`
- Create: `src/Audeu.EplanAi.LegacyHost/Pipe/RemoteGatewayBridgeExecutor.cs`
- Create: `src/Audeu.EplanAi.LegacyHost/Pipe/LegacyHostPipeServer.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/LegacyHostPipeServerTests.cs`

**Interfaces:**
- Consumes `BridgeRequest` and returns `Task<BridgeResponse>`.
- Produces a single-instance server loop that handles one request per connection and serializes execution.

- [ ] Write failing tests for one request/response, concurrent clients serialized, malformed/oversize input never reaching executor and cancellation ending the loop.
- [ ] Confirm RED.
- [ ] Implement one secured `NamedPipeServerStream` instance at a time and executor invocation.
- [ ] Confirm GREEN.

### Task 4: Persistent LegacyHost composition and graceful shutdown

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Hosting/PersistentLegacyHost.cs`
- Modify: `src/Audeu.EplanAi.LegacyHost/Program.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/PersistentLegacyHostTests.cs`

**Interfaces:**
- Production path constructs `EplanRemoteClientAdapter -> RemoteGateway -> RemoteGatewayBridgeExecutor -> LegacyHostPipeServer`.
- Ctrl+C/process cancellation stops the pipe loop and disconnects RemoteGateway.

- [ ] Write failing shutdown/lifetime tests behind injected factories/fakes.
- [ ] Confirm RED.
- [ ] Implement composition and cancellation handling while preserving `--task6-probe`.
- [ ] Confirm full GREEN.

### Task 5: Real workstation named-pipe probe

**Files:**
- Create: `scripts/test-task7-named-pipe.ps1`
- Create/modify diagnostic client mode in LegacyHost or a small .NET 8 test client without EPLAN references.
- Update: `docs/PROJECT_STATE.md`

- [ ] Add client-side probe that starts LegacyHost, connects only to fixed pipe name, sends `system.status` then read-only request(s), and validates responses.
- [ ] Verify fresh CI on final head with non-zero test counts.
- [ ] Run on verified EPLAN 2.9.4 workstation.
- [ ] Record exact acceptance evidence in Issue #18 and `PROJECT_STATE.md`; close Issue #18 only after real PASS.

Do not merge any stacked PR without explicit user approval.