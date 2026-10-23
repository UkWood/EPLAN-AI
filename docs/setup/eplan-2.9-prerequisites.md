# EPLAN 2.9 Development Prerequisites — Gate A

This document defines the mandatory workstation checks before EPLAN-dependent implementation is treated as validated.

## 1. Recorded target workstation

Confirmed on 2026-10-23:

- Product: **EPLAN Electric P8 Professional (64-bit)**;
- Version: **2.9 SP1 Update 4**;
- Build: **14773**;
- Platform installation: `C:\Program Files\EPLAN\Platform\2.9.4`;
- EPLAN `Bin` directory / `EplanApi29Dir`: `C:\Program Files\EPLAN\Platform\2.9.4\Bin`.

Expected major/minor target for this project remains **EPLAN Platform 2.9**.

Do not point the project at assemblies from another EPLAN major/minor release.

## 2. API development entitlement

Confirmed on 2026-10-23: the target workstation has **EPLAN API / API Extension** available for the planned compiled add-in path.

If this capability becomes unavailable on the target environment, the add-in path must stop and be re-scoped explicitly; do not silently switch the project to GUI automation.

## 3. Verified EPLAN 2.9 API assemblies

The build uses the MSBuild property:

```text
EplanApi29Dir
```

Confirmed value:

```text
C:\Program Files\EPLAN\Platform\2.9.4\Bin
```

The workstation directory listing confirms the required files.

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

Additional EPLAN API assemblies are present, including `Eplan.EplApi.EServicesu.dll`, `Eplan.EplApi.Guiu.dll`, `Eplan.EplApi.MasterDatau.dll`, `Eplan.EplApi.Starteru.dll`, and `Eplan.EplApi.Systemu.dll`.

Do not copy EPLAN binaries into the Git repository.

## 4. Build with the verified local EPLAN path

From Developer PowerShell / PowerShell on the target workstation:

```powershell
$eplanBin = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'

dotnet build .\src\Audeu.EplanAi.AddIn\Audeu.EplanAi.AddIn.csproj `
  -c Release `
  -p:EplanApi29Dir="$eplanBin"

dotnet build .\src\Audeu.EplanAi.LegacyHost\Audeu.EplanAi.LegacyHost.csproj `
  -c Release `
  -p:EplanApi29Dir="$eplanBin"
```

This positive local build remains an execution check to perform when the branch is tested on the EPLAN workstation.

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

GitHub Actions already verifies this negative guard for both AddIn and LegacyHost.

## 6. Minimal add-in load check — Gate A2

When Task 4 supplies the minimal add-in:

1. build it against the verified 2.9 assemblies;
2. register/load it using the normal EPLAN 2.9 API add-in workflow;
3. start EPLAN normally with Remoting enabled;
4. confirm the add-in lifecycle is invoked;
5. confirm action `AudeuAiDispatch` is registered.

This is a runtime integration check and cannot be completed before Task 4 supplies the implementation.

## 7. Remoting check — Gate A2

EPLAN 2.9 Remoting uses the EPLAN RemoteClient libraries. Task 4/6 integration must confirm that a small client can:

1. discover or address the local EPLAN instance;
2. connect;
3. ping/verify connectivity;
4. execute the fixed `AudeuAiDispatch` action.

The default port documented by EPLAN 2.9 is `49155`, but implementation should prefer discovery/configuration rather than assume a hard-coded port.

## 8. Gate A record

Gate A is tracked in two parts so implementation is not blocked by runtime checks whose code does not exist yet.

### Gate A1 — development environment

| Check | Result | Evidence / value |
|---|---|---|
| Exact EPLAN 2.9 build recorded | PASS | Electric P8 Professional 64-bit, 2.9 SP1 Update 4, build 14773 |
| API/API Extension entitlement confirmed | PASS | User confirmed available on target workstation |
| `EplanApi29Dir` recorded | PASS | `C:\Program Files\EPLAN\Platform\2.9.4\Bin` |
| Required AddIn DLLs present | PASS | `AFu`, `Baseu`, `DataModelu`, `HEServicesu` present in supplied directory listing |
| Required Remoting DLLs present | PASS | `RemoteClientu`, `Remotingu` present in supplied directory listing |

**Gate A1 outcome: PASS.** Tasks 2 and 3 may proceed.

### Gate A2 — runtime integration

| Check | Result | Evidence / value |
|---|---|---|
| Positive AddIn build against verified assemblies | Pending | Target-workstation execution |
| Add-in can load | Pending | Task 4 |
| `AudeuAiDispatch` registers | Pending | Task 4 |
| RemoteClient can connect | Pending | Task 4/6 |
| Fixed action round-trip succeeds | Pending | Task 4/6 |

**Gate A2 outcome: PENDING by design.** It becomes executable during Tasks 4–6 and is required before Gate B / M0 is accepted.
