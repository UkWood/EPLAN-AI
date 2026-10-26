# Task 9 / Gate B — Real MCP-to-EPLAN Workstation Validation

This is the final M0 read-only acceptance gate. It validates the complete path through an **official MCP stdio client** on the real EPLAN 2.9.4 workstation.

## What this proves

```text
Task 9 probe
  -> MCP stdio
  -> Audeu.EplanAi.Mcp
  -> Audeu.EplanAi.LegacyHost.v1
  -> LegacyHost / EPLAN RemoteClient
  -> AudeuAiDispatch
  -> EPLAN DataModel
```

No write operation is used.

## Preconditions

1. Checkout `m0/task-9-gate-b`.
2. EPLAN Electric P8 2.9.4 is installed and can be started normally.
3. Use the disposable/safe test project. Current known acceptance project:
   - name: `УСПД Жуковский ПНС`;
   - expected pages: `7`.
4. In EPLAN select **exactly one Function** with a known, non-empty Function Text.
5. Note the Function Text exactly. The probe compares it ordinally, including spaces and non-ASCII characters.

## Build verification

In PowerShell:

```powershell
cd E:\EPLAN\REPO\EPLAN-AI
git fetch origin
git switch m0/task-9-gate-b
git pull --ff-only origin m0/task-9-gate-b
.\scripts\verify-task9-build.ps1
```

The script deliberately prepends `C:\Program Files\dotnet` when present because this workstation has previously resolved the x86 `dotnet.exe` first. It requires a visible .NET 8 SDK, builds LegacyHost against the real EPLAN 2.9.4 RemoteClient assemblies, then builds the MCP server and official Task 9 probe.

Expected final lines include three PASS entries for LegacyHost, MCP server and Task 9 probe.

## Window 1 — persistent LegacyHost

Leave EPLAN open with the safe project active and the chosen Function selected.

```powershell
cd E:\EPLAN\REPO\EPLAN-AI
.\scripts\run-task7-legacyhost.ps1
```

Leave this window open.

## Window 2 — official MCP Gate B probe

Either pass the exact Function Text directly:

```powershell
cd E:\EPLAN\REPO\EPLAN-AI
.\scripts\test-task9-mcp-readonly.ps1 -ExpectedFunctionText 'EXACT FUNCTION TEXT HERE'
```

or omit the parameter and enter it when prompted:

```powershell
.\scripts\test-task9-mcp-readonly.ps1
```

The defaults are project `УСПД Жуковский ПНС` and page count `7`. Override them only if intentionally using a different safe project.

## Automatic first phase

Before asking you to touch EPLAN, the probe must report PASS for:

- discovery of exactly five approved tools;
- `eplan_status` = ready / protocol 1.0;
- active project identity and session;
- page list/count;
- exactly one selected `Function`;
- `eplan_get_object` with exact Function Text match.

If any item differs, the probe exits non-zero and Gate B does not pass.

## Closed-project phase

The probe then prints:

```text
ACTION REQUIRED: close the active project in EPLAN so that no project is active.
When the project is fully closed, press Enter here.
```

Close the project **without closing EPLAN itself**. Do not reopen it yet. Once no project is active, return to Window 2 and press Enter.

The probe must report:

```text
PASS: closed-project MCP read returned NO_ACTIVE_PROJECT and cleared the old project session
```

This intermediate call is intentional: it forces `ProjectSessionTracker.Clear()` and makes the following stale-reference check deterministic.

## Reopen phase

The probe then asks you to reopen the same safe project. Reopen it in EPLAN and wait until it is fully active, then press Enter in Window 2.

The probe must establish a fresh `projectSessionId`, then call `eplan_get_object` with the **old** session/reference and require:

```text
SESSION_STALE:
```

Expected final lines:

```text
PASS: old Function reference rejected through MCP with SESSION_STALE
PASS: Gate B real MCP-to-EPLAN read-only acceptance completed successfully.
PASS: Task 9 Gate B probe exited cleanly.
```

## Final shutdown

Return to Window 1 and press `Ctrl+C`.

Expected: LegacyHost exits cleanly, as already proven in Task 7.

## Gate B acceptance criteria

Gate B is PASS only when all are true:

1. exactly five read-only MCP tools are discovered;
2. EPLAN status is ready / protocol 1.0;
3. expected active project is returned;
4. expected page count/list is returned;
5. selected Function and exact Function Text are returned;
6. closed-project call returns `NO_ACTIVE_PROJECT`;
7. reopened project gets a fresh session;
8. old object reference returns `SESSION_STALE`;
9. probe exits 0 and LegacyHost shuts down cleanly.

Only after this evidence is recorded may Task 10 / Gate C write work begin.
