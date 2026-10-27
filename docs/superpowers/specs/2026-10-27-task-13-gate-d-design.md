# Task 13 / Gate D — v0.1 Release Packaging Design

## Intent

Task 13 turns the already accepted M0 + M1 implementation into a reproducible EPLAN AI Bridge v0.1 release without expanding the model-callable surface or changing EPLAN mutation behavior.

The release must be buildable on the verified EPLAN 2.9.4 workstation, easy to install from extracted files, diagnosable when something is wrong, and auditable against the closed-world security rules that governed Tasks 1–12.

## Goals

- build release binaries from source against the verified real EPLAN 2.9.4 assemblies;
- package AddIn, LegacyHost, and MCP server as separate versioned ZIP archives;
- produce one combined release ZIP plus SHA-256 checksums;
- provide installation and troubleshooting documentation usable without the source tree;
- provide repeatable repo-side packaging/security checks in CI;
- provide a final workstation Gate D verification script;
- preserve the recorded Gate B and Gate C real-workstation acceptance evidence;
- prepare, but do not create without explicit approval, tag `eplan-ai-bridge-v0.1.0`.

## Non-goals

- no MSI/MSIX/Setup.exe installer in v0.1;
- no automatic EPLAN project mutation during installation;
- no new MCP tools, bridge operations, EPLAN properties, or write targets;
- no automatic schematic generation, macro insertion, connection creation, page creation/deletion, or device/article selection;
- no network service or TCP listener;
- no bundling of EPLAN DataModel/HEServices assemblies with the AddIn.

## Constraints

- target EPLAN: Electric P8 Professional 64-bit 2.9 SP1 Update 4 build 14773;
- verified API directory: `C:\Program Files\EPLAN\Platform\2.9.4\Bin`;
- AddIn and LegacyHost target .NET Framework 4.7.2;
- MCP targets .NET 8 and is published framework-dependent for `win-x64`;
- scripts must parse and run under Windows PowerShell 5.1;
- release output lives under ignored `artifacts\release\` and is never committed;
- release scripts must fail closed when the real EPLAN API directory is unavailable;
- release/tag creation remains separate from merge approval.

## Approaches considered

### 1. Full Windows installer

Pros: familiar deployment UX and central uninstall support.

Cons: introduces installer technology, privilege/elevation decisions, registry/path behavior, signing concerns, and a new failure surface at the exact point where v0.1 should freeze functionality.

Decision: reject for v0.1.

### 2. One monolithic ZIP

Pros: simplest artifact set.

Cons: obscures component boundaries, makes partial replacement harder, and weakens auditability of AddIn vs LegacyHost vs MCP dependencies.

Decision: retain a combined ZIP for convenience, but not as the only artifact.

### 3. Three component ZIPs plus one combined bundle — selected

Each component is independently inspectable. The combined bundle provides convenience. A SHA-256 manifest covers all release archives. This matches the architectural boundaries already proven by Gate B and Gate C and requires no new runtime technology.

## Release layout

Workstation build output:

```text
artifacts\release\eplan-ai-bridge-v0.1.0\
  Audeu.EplanAi.AddIn-0.1.0.zip
  Audeu.EplanAi.LegacyHost-0.1.0.zip
  Audeu.EplanAi.Mcp-0.1.0-win-x64.zip
  EplanAiBridge-v0.1.0.zip
  SHA256SUMS.txt
```

The combined archive contains:

```text
EplanAiBridge-v0.1.0\
  addin\
  legacyhost\
  mcp\
  docs\INSTALLATION.md
  docs\TROUBLESHOOTING.md
  docs\SECURITY.md
  VERSION.txt
```

### AddIn package

Contains the built AddIn and project-owned dependencies required beside it, including `Audeu.EplanAi.Contracts.dll`. It must not redistribute EPLAN DataModel/HEServices/AF/Base assemblies; those remain resolved from the installed EPLAN platform.

### LegacyHost package

Contains the built LegacyHost, project-owned dependencies, executable configuration if emitted, and the EPLAN RemoteClient/Remoting assemblies copied by the verified real-API build because those references are private runtime dependencies.

### MCP package

Produced by `dotnet publish` for `win-x64` with `--self-contained false`, version `0.1.0`. The target workstation therefore requires the x64 .NET 8 runtime or SDK.

## Script decomposition

### `scripts/New-Task13ReleaseBundle.ps1`

Pure packaging helper. It accepts already-built AddIn, LegacyHost, and MCP input directories plus version/output paths. It validates required primary executables/DLLs, builds the component ZIPs and combined ZIP, and writes deterministic sorted SHA-256 entries. It does not compile source and therefore cannot by itself certify a release build.

This split makes the packaging logic testable in GitHub Actions without EPLAN assemblies.

### `scripts/test-task13-package-layout.ps1`

CI smoke test. It creates disposable fixture component directories, invokes the packaging helper, and verifies exact archive names, required combined-bundle paths, checksum coverage, and rejection of a missing primary binary.

### `scripts/build-task13-release.ps1`

Workstation release orchestrator. It:

1. requires the verified real EPLAN API directory;
2. requires EPLAN and the persistent LegacyHost to be stopped so binaries are not stale/locked;
3. prepends the x64 .NET host when needed;
4. rebuilds AddIn and LegacyHost Release outputs against the real EPLAN 2.9 assemblies with version `0.1.0`;
5. publishes MCP Release for `win-x64`, framework-dependent, version `0.1.0`;
6. rejects EPLAN DataModel/HEServices/AF/Base DLLs in the AddIn staging directory;
7. invokes `New-Task13ReleaseBundle.ps1`;
8. verifies every checksum it just wrote;
9. prints the exact release directory and PASS lines.

### `scripts/verify-task13-security.ps1`

Static source guard for the explicit Gate D security review. It scans `src` and fails if the release source introduces forbidden network listeners, arbitrary code-loading/execution primitives, or the prohibited model-controlled generic input concepts. It complements, rather than replaces, the existing exact-tool and fixed-operation tests.

The script accepts an alternate source root so a CI test can prove both positive and negative behavior.

### `scripts/test-task13-security-verifier.ps1`

Creates safe and intentionally forbidden fixture source trees, proving that the security verifier passes the safe fixture and rejects each forbidden class.

### `scripts/verify-task13-gate-d.ps1`

Final workstation gate. It runs the complete repo-side test suite, the Task 13 security verifier, and the real-API release build. It then checks the package set and checksum manifest. Gate B/Gate C real acceptance is not silently re-simulated: the script points to the accepted evidence and, if production source changed after Gate C, requires those acceptance probes to be repeated before Gate D can be marked PASS.

## Installation model

The release is portable-by-directory. The operator extracts `EplanAiBridge-v0.1.0.zip` to a stable local directory. The AddIn is registered with the official EPLAN `EplApiModuleAction /register:<addin dll>` path. LegacyHost is started as the persistent local gateway. The MCP client is configured to launch the packaged MCP executable over stdio. No inbound network port is opened.

## Troubleshooting model

Documentation is organized by boundary, matching the architecture:

1. AddIn registration/load;
2. EPLAN RemoteClient/LegacyHost connection;
3. named pipe availability/current-user boundary;
4. MCP stdio process and tool discovery;
5. stale project/session errors;
6. safe-write Change Plan lifecycle and undo/rollback;
7. .NET x64 host/runtime selection;
8. checksum/release integrity.

Known stable errors (`NO_ACTIVE_PROJECT`, `SESSION_STALE`, `PLAN_NOT_FOUND`, `PLAN_STALE`, `PLAN_ALREADY_APPLIED`) are documented as diagnostic signals rather than generic failures.

## Security review criteria

Gate D is PASS only if review confirms all of the following remain true:

- no arbitrary EPLAN action forwarding;
- no arbitrary EPLAN property-ID input;
- no TCP listener or web listener in bridge source;
- no model-controlled arbitrary filesystem path;
- no arbitrary code execution, dynamic assembly loading, shell/PowerShell execution, or generic process execution from MCP inputs;
- exactly the approved eight MCP tools remain exposed;
- the only v0.1 write target remains Function Text through immutable Change Plans.

## Release/tag boundary

Task 13 may produce a ready PR and verified release artifacts. The Git tag `eplan-ai-bridge-v0.1.0` and any GitHub Release are created only after explicit user approval, and only from the final verified `main` commit after Task 13 has been merged.