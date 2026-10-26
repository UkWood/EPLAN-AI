# Task 6 local validation — EPLAN RemoteClient

Target workstation: EPLAN Electric P8 Professional 64-bit 2.9 SP1 Update 4 (2.9.4), API path `C:\Program Files\EPLAN\Platform\2.9.4\Bin`.

Task 6 validates a **persistent** LegacyHost -> EPLAN Remoting connection. Unlike Tasks 4/5 `/Auto` probes, EPLAN must already be running and remain open for the entire sequence.

## 1. Update the Task 6 branch

```powershell
git fetch origin
git switch --track origin/m0/task-6-remoteclient
```

If the local branch already exists:

```powershell
git switch m0/task-6-remoteclient
git pull
```

## 2. Build against the real EPLAN 2.9.4 RemoteClient assemblies

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\verify-task6-build.ps1
```

Required result:

```text
PASS: Task 6 LegacyHost compiled against real EPLAN 2.9.4 RemoteClient assemblies: ...\Audeu.EplanAi.LegacyHost.exe
```

If the build fails on an EPLAN API member/signature, stop. The installed 2.9.4 DLLs are authoritative; fix that exact mismatch before runtime testing.

## 3. Start EPLAN normally

1. Start EPLAN Electric P8 normally (do not use `/Auto` and do not use `/NoRemoting`).
2. Open the safe test project used for Task 5.
3. Leave EPLAN running and idle.
4. If practical, select a Function before the probe; this allows the optional `object.get` part to run. An empty/non-Function selection is not a failure for the core Task 6 acceptance.

## 4. Run the persistent RemoteClient probe

In a second PowerShell window at the repository root:

```powershell
.\scripts\test-task6-remoteclient.ps1
```

The LegacyHost process must:

1. discover an active local 64-bit Electric P8 2.9 server;
2. connect using the discovered server port;
3. set `SynchronousMode = true`;
4. ping successfully;
5. call `system.status` through the fixed `AudeuAiDispatch /REQUESTID:<guid>` action;
6. call `project.getActive` and capture its `projectSessionId`;
7. call `project.listPages` with that same session ID;
8. call `selection.get` with that same session ID;
9. if a Function is selected, call `object.get` using the returned session-scoped reference;
10. disconnect cleanly.

Expected core output includes:

```text
PASS: RemoteClient connected and pinged an active local EPLAN 2.9 server.
PASS: RemoteClient SynchronousMode = True
PASS: system.status via RemoteClient
PASS: project.getActive via persistent RemoteClient session
PASS: project.listPages used the same projectSessionId
PASS: selection.get used the same projectSessionId
PASS: Task 6 persistent RemoteClient read sequence completed through selection.get.
PASS: Task 6 RemoteClient probe completed successfully.
```

With a selected Function it should additionally include:

```text
PASS: object.get resolved the selected Function in the same project session
PASS: Task 6 persistent RemoteClient read sequence completed.
```

## Failure interpretation

- `EPLAN_NOT_FOUND`: no eligible active local 64-bit Electric P8 2.9 Remoting server was discovered. Confirm EPLAN was started normally without `/NoRemoting` and remains running.
- `EPLAN_NOT_CONNECTED`: discovery succeeded but connect/ping failed or the connection was lost.
- `EPLAN_ACTION_FAILED`: EPLAN rejected or failed the fixed `AudeuAiDispatch` action.
- `OPERATION_TIMEOUT`: synchronous action execution exceeded the host timeout; the gateway deliberately drops the connection and requires reconnect.
- `SESSION_STALE` on `project.listPages`/`selection.get`: the persistent-session invariant is broken and Task 6 must not be accepted.
- assembly load failure: preserve the exact exception; Task 6 build may need an explicit standalone deployment/probing rule for EPLAN RemoteClient dependencies.

Do not merge the Task 6 PR until the real build and persistent RemoteClient probe pass.