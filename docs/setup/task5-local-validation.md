# Task 5 local validation — real EPLAN 2.9.4

This procedure validates the Task 5 production AddIn against the real workstation EPLAN API and proves the first read-only DataModel operation (`project.getActive`) through the existing `AudeuAiDispatch` spool path.

## Preconditions

- Worktree/repository: `E:\EPLAN\REPO\EPLAN-AI`.
- Branch: `m0/task-5-readonly-inspection`.
- EPLAN API: `C:\Program Files\EPLAN\Platform\2.9.4\Bin`.
- The x64 `dotnet` host resolves from `C:\Program Files\dotnet`.
- Task 4 AddIn registration is already proven.
- Use a safe non-production EPLAN project for this read-only check.

## 1. Pull the Task 5 branch

```powershell
cd E:\EPLAN\REPO\EPLAN-AI
git switch m0/task-5-readonly-inspection
git pull
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

## 2. Build against the real EPLAN 2.9.4 assemblies

EPLAN may be closed or open for the build itself, but close it before the runtime smoke test.

```powershell
.\scripts\verify-task5-build.ps1
```

Expected result:

```text
PASS: Task 5 AddIn compiled against real EPLAN 2.9.4 assemblies: ...\Audeu.EplanAi.AddIn.dll
```

If the real build finds an EPLAN 2.9.4 signature mismatch, stop here and preserve the complete compiler output. Treat the installed workstation assembly as authoritative.

## 3. Ensure EPLAN has a last-opened test project

The command-line smoke test uses EPLAN 2.9 `/UseLastOpenedProjects:1` so that a project is available when the action executes.

If needed:

1. start EPLAN normally;
2. open a safe test project;
3. wait until it is fully open;
4. close EPLAN normally.

Do not leave EPLAN running before the next command.

## 4. Run `project.getActive` through the real AddIn

```powershell
.\scripts\test-task5-active-project.ps1
```

If the EPLAN License Manager dialog appears during command-line startup, confirm the existing license selection the same way as in Task 4 (Enter was sufficient on the verified workstation).

Expected output includes:

```text
PASS: Task 5 project.getActive succeeded for request <guid>
  projectSessionId : <guid>
  databaseId      : <non-zero uint>
  name            : <project name>
  fullName        : <project path/name>
  pageCount       : <count>
  isReadOnly      : <True|False>
```

If the result is `NO_ACTIVE_PROJECT`, open the test project normally once, close EPLAN normally, and rerun the probe. `/UseLastOpenedProjects:1` is already enabled by the script.

## Why this Task 5 smoke test does not chain `listPages` / `selection.get` / `object.get`

`projectSessionId` is intentionally scoped to one live EPLAN/AddIn runtime. Every clean `/Auto` command-line invocation starts a new EPLAN process and therefore a new session. Reusing the previous process's session ID in a second invocation would correctly fail with `SESSION_STALE`.

Do **not** weaken that safety rule for a CLI test.

The repo-side Task 5 tests already cover:

- stable session reuse inside one runtime;
- session replacement when `DatabaseIdentifier` changes;
- `NO_ACTIVE_PROJECT` and `SESSION_STALE`;
- page projection;
- empty/current SelectionSet projection;
- malformed/missing/invalid object references;
- Function-only `object.get` whitelist.

Persistent end-to-end page/selection/object validation is performed in Task 6 when `LegacyHost` / `EplanRemoteClient` keeps the same EPLAN runtime alive across multiple bridge calls.

## Evidence to preserve

Preserve and report the complete output of:

```powershell
.\scripts\verify-task5-build.ps1
.\scripts\test-task5-active-project.ps1
```

Task 5 remains open until both real-workstation checks pass.
