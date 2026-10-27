# EPLAN AI Bridge v0.1.0 — Troubleshooting

Troubleshoot from the EPLAN boundary outward. The bridge is deliberately layered, so a failure at one layer should be corrected there instead of bypassing the security boundary.

## 1. AddIn registration or load failure

### Symptoms

- `AudeuAiDispatch` is unavailable;
- LegacyHost connects to EPLAN but dispatch calls fail;
- EPLAN reports an API module load/registration error.

### Checks

1. Confirm the AddIn DLL exists at the exact path used during registration.
2. Confirm EPLAN is the verified 64-bit 2.9 SP1 Update 4 build 14773 installation.
3. Confirm API/API Extension is installed.
4. Re-register with the official `EplApiModuleAction /register:<dll>` flow documented in `INSTALLATION.md`.
5. Do not copy EPLAN `DataModel`, `HEServices`, `AF`, or `Base` DLLs into the AddIn directory. The release intentionally resolves them from the installed EPLAN platform.

## 2. LegacyHost cannot reach EPLAN

### Symptoms

- `EPLAN_NOT_FOUND`;
- `EPLAN_NOT_CONNECTED`;
- RemoteClient discovery/connect error;
- LegacyHost exits immediately.

### Checks

1. Start EPLAN normally before LegacyHost.
2. Ensure EPLAN is running in the same Windows user session.
3. Put the verified EPLAN API directory at the front of `PATH` before launching LegacyHost:

```powershell
$env:Path = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin;' + $env:Path
```

4. Confirm the LegacyHost directory contains its packaged runtime dependencies, including EPLAN RemoteClient/Remoting assemblies.
5. Confirm the AddIn is registered and loaded.

Do not replace RemoteClient with direct EPLAN assembly access from the MCP process; that would violate the v0.1 architecture.

## 3. MCP cannot connect to the local bridge

### Symptoms

- MCP tool call reports bridge connection failure or timeout;
- LegacyHost window is not running;
- named-pipe connection fails.

### Checks

1. LegacyHost must remain running in its own console.
2. The fixed pipe name is:

```text
Audeu.EplanAi.LegacyHost.v1
```

3. MCP and LegacyHost must run as the same Windows user.
4. Do not configure a TCP host/port; v0.1 does not expose a network listener.
5. Restart in order: EPLAN -> LegacyHost -> MCP process.

## 4. MCP executable does not start

### Symptoms

- Windows reports a missing .NET runtime;
- client starts the command and it exits before tool discovery;
- wrong `dotnet.exe` is selected.

### Checks

Use the x64 host explicitly:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --list-runtimes
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

The MCP release requires x64 .NET 8. On the verified workstation, `C:\Program Files (x86)\dotnet` can appear earlier in `PATH`; do not diagnose the x64 installation using that host.

## 5. Tool discovery is not exactly eight tools

Expected v0.1 tools:

```text
eplan_status
eplan_get_active_project
eplan_list_pages
eplan_get_selection
eplan_get_object
eplan_preview_set_function_text
eplan_apply_change_plan
eplan_undo_last_ai_change
```

If discovery differs:

- verify the client launches the v0.1.0 MCP executable from the intended bundle;
- stop any older MCP process;
- verify `VERSION.txt` in the bundle;
- verify the release ZIP SHA-256.

Do not enable an unknown tool or generic EPLAN action/property interface as a workaround.

## 6. Project/session stable errors

### `NO_ACTIVE_PROJECT`

No EPLAN project is currently active. Open the intended project and request a fresh active-project result.

### `SESSION_STALE`

The `projectSessionId` belongs to an older active-project session. This is expected after closing/reopening or switching the project. Obtain a new active project/session and new object references.

Object references from a stale session must not be reused or rewritten manually.

## 7. Object inspection errors

### `OBJECT_NOT_FOUND`

The referenced EPLAN object no longer resolves in the current session.

### `OBJECT_INVALID`

The resolved object is no longer valid.

### `OBJECT_TYPE_NOT_ALLOWED`

The requested operation does not accept that object type. The v0.1 write workflow is limited to `Function` Function Text.

Refresh selection/inspection rather than changing type/database/object identifiers by hand.

## 8. Change Plan stable errors

### `PLAN_NOT_FOUND`

The plan does not exist in the current MCP process. v0.1 Change Plans are in-memory only and do not survive MCP restart. Preview again.

### `PLAN_STALE`

The current project/session/object/value no longer matches the immutable preview. Re-inspect the target and create a new preview. Do not try to patch the old plan.

### `PLAN_ALREADY_APPLIED`

The plan was already claimed for apply. Plans are single-use. Verify the current Function Text and create a new preview for any further change.

The apply tool accepts only `changePlanId`; target and values cannot be resubmitted during apply.

## 9. `READ_ONLY_PROJECT` or validation failure

A write was rejected before mutation because the project/Function is read-only or an input/precondition failed. Correct the EPLAN state, re-inspect, and create a fresh preview.

Do not bypass the check by editing bridge payloads.

## 10. Apply fails or post-condition is not verified

The AddIn write path uses EPLAN `UndoStep`, `SafetyPoint`, and `Transaction`, then reads displayed Function Text back before commit. An exception, mismatch, or unsafe implicit EPLAN transaction condition fails closed.

If an apply reports failure:

1. read the Function again;
2. confirm whether the original text is preserved;
3. discard the old plan;
4. correct the underlying EPLAN condition before previewing again.

Gate C explicitly verified rollback after a controlled failure between assignment and commit.

## 11. Undo is rejected

`eplan_undo_last_ai_change` is scoped to the current project session and the last successfully applied AI Function Text change. A project/session change invalidates the stored undo state.

If undo cannot be safely proven, the bridge rejects it rather than applying a guessed reversal.

## 12. Release checksum mismatch

Do not run mismatched artifacts.

Recompute:

```powershell
Get-FileHash .\*.zip -Algorithm SHA256 | Sort-Object Path
Get-Content .\SHA256SUMS.txt
```

If one byte differs, obtain/rebuild the release bundle again. `build-task13-release.ps1` verifies its own manifest before reporting PASS.

## 13. Gate D release build refuses to start

The builder intentionally fails early with stable release codes.

- `RELEASE_EPLAN_API_MISSING` — verified EPLAN API directory or required assembly missing.
- `RELEASE_EPLAN_RUNNING` — close EPLAN before compiling/package staging.
- `RELEASE_LEGACYHOST_RUNNING` — stop persistent LegacyHost before compiling/package staging.
- `RELEASE_DOTNET8_MISSING` — x64 .NET 8 SDK is not visible.
- `RELEASE_DOC_MISSING` — required release documentation is absent.
- `RELEASE_ADDIN_EPLAN_DLL_FORBIDDEN` — packaging would redistribute prohibited EPLAN platform DLLs with the AddIn.
- `RELEASE_CHECKSUM_MISMATCH` — produced archive no longer matches the manifest.

These are safety guards, not conditions to suppress.

## 14. Recommended restart sequence

For most local runtime issues:

1. stop the MCP client/process;
2. press `Ctrl+C` in LegacyHost and confirm it exits cleanly;
3. close EPLAN;
4. start EPLAN and open the intended project;
5. start LegacyHost;
6. reconnect/start the MCP client;
7. call `eplan_status` and obtain a fresh `projectSessionId`.

This intentionally discards old in-memory Change Plans and stale project references.