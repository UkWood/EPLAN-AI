# Task 4 local validation — EPLAN 2.9

This validation must be run on the verified workstation with EPLAN Electric P8 Professional 2.9 SP1 Update 4 build 14773 and API/API Extension available.

## 1. Build against the real EPLAN API assemblies

From the repository root on branch `m0/task-4-dispatch`:

```powershell
.\scripts\verify-task4-build.ps1
```

Expected result:

```text
PASS: real-API AddIn build produced ...\Audeu.EplanAi.AddIn.dll
```

This is the first check that the code compiles against the actual EPLAN 2.9.4 assemblies rather than the CI test stubs.

## 2. Register/load the AddIn

EPLAN 2.9 provides the official `EplApiModuleAction` action for loading and registering an API AddIn.

Close disposable/test EPLAN sessions if multiple instances would be undesirable, then run:

```powershell
.\scripts\verify-task4-build.ps1 -Register
```

The script starts EPLAN/W3u with `EplApiModuleAction /register:<addin dll>`.

Verify that the module loads without an API/module error. Do not test against a production project; Task 4 does not require any project to be open.

## 3. Prove the fixed action + spool round-trip

After the AddIn is registered, run:

```powershell
.\scripts\test-task4-status-roundtrip.ps1
```

The script:

1. generates a canonical GUID request ID;
2. writes only a `system.status` request under `%LOCALAPPDATA%\Audeu\EplanAiBridge\requests`;
3. starts EPLAN with the fixed `AudeuAiDispatch /REQUESTID:<guid>` action;
4. waits for the matching response file;
5. verifies `Success=true` and `status=ready`.

Expected result:

```text
PASS: AudeuAiDispatch system.status round-trip succeeded for <guid>
```

## Gate A2 interpretation

Task 4 can establish these Gate A2 parts:

- positive AddIn build against the real 2.9.4 assemblies;
- AddIn registration/load;
- custom action `AudeuAiDispatch` registration;
- direct command-line `system.status` round-trip through the local spool.

The RemoteClient portion of Gate A2 remains Task 6.

If any step fails, preserve the PowerShell output and EPLAN system message; do not substitute GUI automation or loosen the fixed-action/REQUESTID restrictions.
