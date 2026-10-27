# EPLAN AI Bridge v0.1.0 — Installation

## Supported target

Release `0.1.0` is verified for:

- EPLAN Electric P8 Professional 64-bit;
- EPLAN 2.9 SP1 Update 4, build 14773;
- EPLAN API/API Extension installed;
- EPLAN API directory `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- Windows x64;
- x64 .NET 8 runtime (or SDK) for the MCP server.

Other EPLAN versions are not part of the v0.1 acceptance boundary.

## Release files

A verified release directory contains exactly:

```text
Audeu.EplanAi.AddIn-0.1.0.zip
Audeu.EplanAi.LegacyHost-0.1.0.zip
Audeu.EplanAi.Mcp-0.1.0-win-x64.zip
EplanAiBridge-v0.1.0.zip
SHA256SUMS.txt
```

The combined archive is the normal installation artifact. The three component archives are provided for inspection or controlled component replacement.

## 1. Verify SHA-256 before extraction

Open Windows PowerShell in the release directory:

```powershell
Get-Content .\SHA256SUMS.txt
Get-FileHash .\*.zip -Algorithm SHA256 | Sort-Object Path
```

Every ZIP hash must exactly match the corresponding entry in `SHA256SUMS.txt`. Do not install a bundle with a missing or mismatched checksum.

## 2. Extract the combined bundle

Use a stable, current-user directory. Example:

```powershell
$installRoot = Join-Path $env:LOCALAPPDATA 'Audeu\EPLAN-AI\0.1.0'
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Expand-Archive .\EplanAiBridge-v0.1.0.zip -DestinationPath $installRoot -Force
```

The executable bundle root is then normally:

```text
%LOCALAPPDATA%\Audeu\EPLAN-AI\0.1.0\EplanAiBridge-v0.1.0
```

It contains:

```text
addin\
legacyhost\
mcp\
docs\
VERSION.txt
```

Do not move individual DLLs out of their component directories.

## 3. Register the EPLAN AddIn

The AddIn must be registered with the official EPLAN API module action. Close EPLAN first.

Set paths for your installation:

```powershell
$bundle = Join-Path $env:LOCALAPPDATA 'Audeu\EPLAN-AI\0.1.0\EplanAiBridge-v0.1.0'
$addin = Join-Path $bundle 'addin\Audeu.EplanAi.AddIn.dll'
$eplan = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin\EPLAN.exe'
```

Register:

```powershell
& $eplan `
  '/Variant:"Electric P8"' `
  '/NoLoadWorkspace' `
  '/NoSplash' `
  '/Auto' `
  'EplApiModuleAction' `
  "/register:`"$addin`""
```

If your EPLAN executable is named or installed differently, use the executable from the same verified EPLAN 2.9.4 installation. The AddIn package intentionally does **not** redistribute EPLAN DataModel, HEServices, AF, or Base assemblies; those are resolved from the installed EPLAN platform.

After registration, start EPLAN normally. The fixed action `AudeuAiDispatch` must be available to the local bridge.

## 4. Start the persistent LegacyHost

EPLAN must already be running before LegacyHost starts.

In a dedicated PowerShell window:

```powershell
$bundle = Join-Path $env:LOCALAPPDATA 'Audeu\EPLAN-AI\0.1.0\EplanAiBridge-v0.1.0'
$eplanApi = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
$env:Path = "$eplanApi;$env:Path"
& (Join-Path $bundle 'legacyhost\Audeu.EplanAi.LegacyHost.exe')
```

Leave this window open while the MCP client is in use. LegacyHost owns the only EPLAN `RemoteClient` connection and exposes the fixed current-user named pipe:

```text
Audeu.EplanAi.LegacyHost.v1
```

To stop the bridge, stop the MCP client first and press `Ctrl+C` in the LegacyHost window. A normal stop should return cleanly without an unhandled exception.

## 5. Configure the MCP client

Configure your MCP client to launch this executable over **stdio**:

```text
<bundle>\mcp\Audeu.EplanAi.Mcp.exe
```

No TCP host or port is required. The MCP server connects only to the fixed local named pipe owned by LegacyHost.

The MCP process is deliberately framework-dependent. If it does not start, verify the x64 .NET 8 runtime:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --list-runtimes
```

At least one `Microsoft.NETCore.App 8.x` runtime must be visible to the x64 host.

## 6. First non-mutating smoke check

Before using any write operation:

1. start EPLAN normally;
2. open the intended project;
3. start LegacyHost;
4. start/connect the MCP client;
5. verify MCP discovery exposes exactly these eight tools:

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

6. call `eplan_status` and verify the bridge reports ready/protocol `1.0`;
7. call `eplan_get_active_project` and a read-only inspection tool before attempting a preview.

`eplan_preview_set_function_text` is itself read-only with respect to EPLAN; it creates an in-memory immutable Change Plan. `eplan_apply_change_plan` is the destructive step and accepts only the returned `changePlanId`.

## 7. Safe write usage

The only v0.1 write workflow is Function Text:

```text
inspect Function
  -> preview new Function Text
  -> review immutable old/new values
  -> apply by changePlanId
  -> read back/verify
  -> optionally undo last AI change
```

Do not use a production project for acceptance experiments. Gate C was performed on a disposable project copy.

## Updating v0.1

When replacing a build:

1. stop the MCP process;
2. stop LegacyHost;
3. close EPLAN;
4. verify the new archive hashes;
5. extract to a new version/build directory rather than overwriting files in use;
6. re-register the AddIn if its path changes;
7. restart EPLAN, LegacyHost, and the MCP client in that order.

The v0.1 release does not install a Windows service and does not require an inbound firewall rule.