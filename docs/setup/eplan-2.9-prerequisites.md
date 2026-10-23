# EPLAN 2.9 Development Prerequisites — Gate A

This document defines the mandatory workstation checks before EPLAN-dependent implementation is treated as validated.

## 1. Record the installed EPLAN build

On the target Windows workstation, record:

- EPLAN product/variant;
- exact EPLAN Platform version/build;
- installation directory;
- EPLAN `Bin` directory.

Expected major/minor target for this project: **EPLAN Platform 2.9**.

Do not point the project at assemblies from another EPLAN major/minor release.

## 2. Confirm API development entitlement

Confirm that the target installation/account has the EPLAN API/API Extension capability required to load and execute the planned compiled add-in.

Record the result in `docs/PROJECT_STATE.md`.

If the required API capability is unavailable, Gate A fails. Stop the add-in implementation path and re-scope explicitly; do not silently switch the project to GUI automation.

## 3. Locate the EPLAN 2.9 API assemblies

The build uses the MSBuild property:

```text
EplanApi29Dir
```

It must point to the directory containing the EPLAN 2.9 API assemblies, normally the EPLAN Platform `Bin` directory.

The initial projects require these files.

### Add-in

```text
Eplan.EplApi.AFu.dll
Eplan.EplApi.Baseu.dll
Eplan.EplApi.DataModelu.dll
Eplan.EplApi.HEServicesu.dll
```

### LegacyHost / Remoting

```text
Eplan.EplApi.RemoteClientu.dll
Eplan.EplApi.Remotingu.dll
```

Do not copy these binaries into the Git repository.

## 4. Build with a local EPLAN path

Example from Developer PowerShell / PowerShell:

```powershell
$eplanBin = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'

dotnet build .\src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj `
  -c Release `
  -p:EplanApi29Dir="$eplanBin"

dotnet build .\src\Audeu.EplanAi.LegacyHost\Audeu.EplanAi.LegacyHost.csproj `
  -c Release `
  -p:EplanApi29Dir="$eplanBin"
```

The example path is illustrative only. Use the actual installed EPLAN 2.9 `Bin` directory.

## 5. Negative guard check

Run an EPLAN-dependent build without `EplanApi29Dir`:

```powershell
dotnet build .\src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj -c Release
```

Expected failure contains exactly:

```text
EplanApi29Dir must point to the EPLAN 2.9 API assembly directory.
```

This is intentional. It prevents accidental compilation against unknown or mismatched EPLAN assemblies.

## 6. Minimal add-in load check

When Task 4 supplies the minimal add-in:

1. build it against the verified 2.9 assemblies;
2. register/load it using the normal EPLAN 2.9 API add-in workflow;
3. start EPLAN normally with Remoting enabled;
4. confirm the add-in lifecycle is invoked;
5. confirm action `AudeuAiDispatch` is registered.

Gate A is not fully complete until the target workstation can load the add-in.

## 7. Remoting check

EPLAN 2.9 Remoting uses the EPLAN RemoteClient libraries. The first integration check must confirm that a small client can:

1. discover or address the local EPLAN instance;
2. connect;
3. ping/verify connectivity;
4. execute the fixed `AudeuAiDispatch` action.

The default port documented by EPLAN 2.9 is `49155`, but implementation should prefer discovery/configuration rather than assume a hard-coded port.

## 8. Gate A record

Update the following table when performed on the real workstation.

| Check | Result | Evidence / value |
|---|---|---|
| Exact EPLAN 2.9 build recorded | Pending | |
| API/API Extension entitlement confirmed | Pending | |
| `EplanApi29Dir` recorded | Pending | |
| Required AddIn DLLs present | Pending | |
| Required Remoting DLLs present | Pending | |
| Add-in can load | Pending | Task 4 |
| RemoteClient can connect | Pending | Task 4/6 |

## Gate A outcome

**Current status: PENDING — requires execution on the actual EPLAN 2.9 workstation.**
