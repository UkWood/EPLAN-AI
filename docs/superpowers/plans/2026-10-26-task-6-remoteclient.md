# Task 6 EPLAN RemoteClient Gateway Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a deterministic LegacyHost RemoteClient gateway that keeps one EPLAN 2.9 runtime connection alive and executes only the fixed bridge action.

**Architecture:** A testable `IRemoteClientAdapter` isolates EPLAN API signatures. `RemoteGateway` owns discovery/selection, connection lifecycle, synchronous execution, timeout/error mapping and the existing spool round-trip. The production adapter is the only LegacyHost class that directly references `EplanRemoteClient`.

**Tech Stack:** C#; .NET Framework 4.7.2; EPLAN 2.9.4 `RemoteClientu`/`Remotingu`; xUnit v3/Microsoft Testing Platform; existing `SpoolStore`.

**Spec:** `docs/superpowers/specs/2026-10-26-task-6-remoteclient-design.md`

## Global Constraints

- Fixed action name: `AudeuAiDispatch`.
- Action payload contains only generated canonical request GUID.
- `SynchronousMode = true` before execution.
- Default operation timeout: 60 seconds.
- No arbitrary host/port/path/action input from callers.
- No named pipe in Task 6.
- No write operations or engineering logic in LegacyHost.
- Production build must compile against the real EPLAN 2.9.4 assemblies.

## Review Focus

1. Multiple active servers -> deterministic eligible EPLAN 2.9 selection.
2. No server / failed ping -> stable connection errors, no action call.
3. Caller operation text cannot alter the outgoing EPLAN action name.
4. Timeout poisons/disconnects the client so a later request cannot reuse an uncertain session.
5. Response request ID must match the request even when stale/late files exist.

---

### Task 1: Add RemoteClient error codes and test-only API boundary

**Files:**
- Modify: `src/Audeu.EplanAi.Contracts/BridgeErrorCodes.cs`
- Create: `src/Audeu.EplanAi.LegacyHost/Remote/RemoteServerInfo.cs`
- Create: `src/Audeu.EplanAi.LegacyHost/Remote/IRemoteClientAdapter.cs`
- Modify: `src/Audeu.EplanAi.LegacyHost/Audeu.EplanAi.LegacyHost.csproj`
- Create: `test-support/EplanRemoteClientStubs.cs`
- Modify: `tests/Audeu.EplanAi.LegacyHost.Tests/Audeu.EplanAi.LegacyHost.Tests.csproj`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/RemoteGatewayContractTests.cs`

**Interfaces:**
- error constants: `EPLAN_NOT_FOUND`, `EPLAN_NOT_CONNECTED`, `EPLAN_ACTION_FAILED`, `OPERATION_TIMEOUT`;
- `IRemoteClientAdapter.DiscoverLocalServers()`;
- `Connect(RemoteServerInfo, TimeSpan)`, `Disconnect()`, `Ping()`, `SynchronousMode`, `ExecuteAction(string)`.

- [ ] Write compile-level contract tests first.
- [ ] Run LegacyHost tests and verify RED because new types/constants are absent.
- [ ] Add minimal contracts/stubs/project wiring.
- [ ] Run LegacyHost tests and verify GREEN.

### Task 2: Implement RemoteGateway policy with fake adapter

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Remote/RemoteGateway.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/RemoteGatewayTests.cs`

**Interfaces:**
- `Connect(TimeSpan timeout) -> BridgeResponse` status envelope;
- `Ping() -> BridgeResponse`;
- `ExecuteBridgeRequestAsync(BridgeRequest, TimeSpan, CancellationToken) -> Task<BridgeResponse>`;
- `Disconnect()`.

- [ ] Write tests for server selection, no server, failed ping, exact action string, action false, timeout and poisoned connection.
- [ ] Run LegacyHost tests and verify RED.
- [ ] Implement minimal policy using existing `SpoolStore`.
- [ ] Run LegacyHost tests and verify GREEN.

### Task 3: Implement real EplanRemoteClientAdapter

**Files:**
- Create: `src/Audeu.EplanAi.LegacyHost/Remote/EplanRemoteClientAdapter.cs`
- Test: `tests/Audeu.EplanAi.LegacyHost.Tests/EplanRemoteClientAdapterContractTests.cs`

**Interfaces:**
- `GetActiveEplanServersOnLocalMachine(out List<EplanServerData>)`;
- `Connect("localhost", port, timeout)`;
- `SynchronousMode`;
- `Ping()`;
- `ExecuteAction(string)`;
- `Disconnect()` / `Dispose()`.

- [ ] Write stub-backed adapter tests first and verify RED.
- [ ] Implement production adapter.
- [ ] Run LegacyHost tests and verify GREEN.
- [ ] Run full CI with non-zero test counts.

### Task 4: Add real-workstation RemoteClient validation harness

**Files:**
- Create: `scripts/verify-task6-build.ps1`
- Create: `scripts/test-task6-remoteclient.ps1`
- Create: `docs/setup/task6-local-validation.md`
- Modify: `docs/PROJECT_STATE.md`

**Interfaces:**
- build probe compiles LegacyHost against real EPLAN 2.9.4 assemblies;
- runtime probe discovers an already-running EPLAN server, connects, pings, sends `system.status`, then uses the returned Task 5 session to call `project.listPages` in the same runtime;
- optional selection/object checks are reported when a Function is selected.

- [ ] Add scripts/docs after repo-side GREEN.
- [ ] Verify fresh head CI.
- [ ] Open stacked draft PR targeting `m0/task-5-readonly-inspection`; do not merge.
- [ ] Keep Issue #16 open until real build + discovery/connect/ping + same-session read sequence pass.

## Self-review

- Spec coverage: discovery, connect/disconnect, ping, sync execution, timeout and fixed action are all mapped.
- Type consistency: server port stays integer internally and becomes invariant string only at EPLAN API call; request ID is `Guid`.
- Security: no action/host/port/path is caller-controlled.
- Real API risk: all exact EPLAN signatures are isolated in one adapter and revalidated on the workstation.
- Task boundary: named pipe remains Task 7.