# Task 9 Gate B Workstation Acceptance Design

**Status:** approved implementation of the existing M0 Gate B requirement  
**Date:** 2026-10-26  
**Issue:** #22  
**Base:** Task 8 / PR #21

## Goal

Prove the complete read-only EPLAN AI Bridge path on the real EPLAN 2.9.4 workstation through an actual MCP stdio client, including deterministic stale-session invalidation after project close/reopen.

## Chosen approach

Use a dedicated .NET 8 console probe under `test-support/` built with the same pinned `ModelContextProtocol` 2.3.0 package. The probe uses the official `StdioClientTransport` to launch `Audeu.EplanAi.Mcp.exe`, performs MCP discovery and tool calls, parses the returned compact JSON, and fails with a non-zero exit code on any acceptance mismatch.

This is preferred over hand-written JSON-RPC because it tests the real SDK handshake, stdio framing, tool discovery, invocation, tool-error semantics, child-process lifecycle and stdout discipline at the same time.

## Workstation topology

```text
Task9 probe (.NET 8 MCP client)
        |
        | official MCP StdioClientTransport
        v
Audeu.EplanAi.Mcp.exe
        |
        | Audeu.EplanAi.LegacyHost.v1
        v
LegacyHost -> EPLAN RemoteClient -> AudeuAiDispatch -> EPLAN 2.9.4
```

LegacyHost remains a separately started persistent process exactly as proven by Task 7. The probe owns the MCP child-process lifecycle.

## Acceptance sequence

The probe accepts:
- path to the built MCP executable;
- expected project name;
- expected page count;
- exact expected Function Text for the single Function selected in EPLAN.

Then it performs:

1. `ListToolsAsync` and requires exactly the five Task 8 tools.
2. `eplan_status` and requires `status=ready`, `protocolVersion=1.0`.
3. `eplan_get_active_project` and requires expected name/page count plus a non-empty `projectSessionId`.
4. `eplan_list_pages` with that session and requires the returned item count to equal the expected page count.
5. `eplan_get_selection` and requires exactly one selected item of `kind=Function` with a complete compact reference.
6. `eplan_get_object` using that reference and requires `kind=Function` and exact `functionText` equality.
7. Prompt: close the project in EPLAN and press Enter only when no project is active.
8. Call `eplan_list_pages` with the old session and require an MCP tool error starting with `NO_ACTIVE_PROJECT:`. This deliberately clears `ProjectSessionTracker`.
9. Prompt: reopen the same safe project and press Enter when it is active again.
10. Call `eplan_get_active_project` and require the expected project identity again; this establishes a fresh session.
11. Call `eplan_get_object` with the old session/reference and require an MCP tool error starting with `SESSION_STALE:`.
12. Dispose the MCP client/transport and report Gate B PASS.

## Why the intermediate closed-project call is mandatory

`ProjectSessionTracker` invalidates a session when it observes no active project or a different database identifier. A close/reopen performed entirely between bridge calls could theoretically reuse the same database identifier. Calling a session-aware tool while the project is closed forces `Clear()`, making the stale-reference assertion deterministic even if the same project/database ID is reused after reopening.

## Workstation wrapper

`scripts/test-task9-mcp-readonly.ps1`:
- keeps Windows PowerShell 5.1 compatibility;
- prepends `C:\Program Files\dotnet` to process PATH when present so x64 SDK wins over the workstation x86 dotnet entry;
- validates EPLAN is running;
- validates the MCP executable and probe executable exist;
- prompts for exact Function Text if not supplied;
- invokes the probe and propagates its exit code.

`scripts/verify-task9-build.ps1` builds:
- the real LegacyHost against `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- the Task 8 MCP server in Release;
- the Task 9 probe in Release.

## CI scope

CI cannot run the real EPLAN acceptance. It will:
- build the probe;
- run unit tests for probe JSON/result parsing and argument/reference extraction where useful;
- continue running all existing suites.

The actual Gate B result exists only after the workstation probe passes all steps.

## Non-goals

- no write operation;
- no EPLAN API reference in MCP or probe;
- no custom MCP wire implementation;
- no HTTP transport;
- no automatic manipulation of the EPLAN UI;
- no Gate C / M1 work before Gate B PASS.
