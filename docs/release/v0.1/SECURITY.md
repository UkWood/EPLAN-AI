# EPLAN AI Bridge v0.1.0 — Security Review

## Security objective

EPLAN AI Bridge v0.1 gives an MCP client useful EPLAN inspection and one narrowly controlled write workflow without exposing unrestricted EPLAN API access to the model.

The design is closed-world: model-callable operations are enumerated, object identity is validated against the current project session, write intent is previewed into an immutable Change Plan, and apply accepts only a server-owned `changePlanId`.

## Trust boundaries

```text
MCP client / model
  -> stdio
Audeu.EplanAi.Mcp (.NET 8)
  -> fixed current-user named pipe
Audeu.EplanAi.LegacyHost (.NET Framework 4.7.2)
  -> EPLAN RemoteClient / fixed AudeuAiDispatch
Audeu.EplanAi.AddIn (.NET Framework 4.7.2)
  -> EPLAN DataModel / HEServices
```

Only `LegacyHost` knows EPLAN `RemoteClient`. Only `AddIn` accesses EPLAN DataModel/HEServices. The MCP process references no EPLAN assembly.

## Approved MCP surface

Release v0.1 exposes exactly eight tools:

### Read-only

```text
eplan_status
eplan_get_active_project
eplan_list_pages
eplan_get_selection
eplan_get_object
```

### Safe Function Text workflow

```text
eplan_preview_set_function_text
eplan_apply_change_plan
eplan_undo_last_ai_change
```

There is no generic `executeAction`, `setProperty`, `invokeOperation`, filesystem, shell, or code-execution tool.

## Function Text write boundary

### Preview

`eplan_preview_set_function_text` accepts a compact Function reference and proposed Function Text. It performs read-only inspection, captures the exact displayed old text, validates the target and text limit, and stores an immutable in-memory Change Plan.

Preview does not mutate EPLAN.

### Apply

`eplan_apply_change_plan` accepts only:

```text
changePlanId
```

The caller cannot resubmit or alter the target, old value, or new value during apply. The plan is atomically claimed so two applies cannot both start.

Inside the EPLAN boundary the implementation revalidates:

- current project/session;
- database/type/object/string identity;
- Function type;
- writability;
- exact displayed old Function Text against the preview snapshot.

The only property mutated is Function `FUNC_TEXT`.

Persistent mutation is bounded by EPLAN `UndoStep`, `SafetyPoint`, and API `Transaction`. The displayed value is read back and verified before commit. Controlled failure or post-condition mismatch before commit leaves the SafetyPoint uncommitted and rolls the change back.

### Undo

`eplan_undo_last_ai_change` accepts only the current `projectSessionId`. It uses guarded last-change state from the successful AI change and rejects uncertain/stale session state instead of guessing a reversal.

## Project-session boundary

Project-scoped references are valid only for the live `projectSessionId` that created them. Closing/reopening or switching projects creates a fresh session. Old references are rejected with stable stale-session errors rather than inferred to point at a new object.

This prevents session-scoped EPLAN identifiers from being treated as durable identity.

## Local transport boundary

MCP uses stdio. MCP-to-LegacyHost communication uses the fixed current-user named pipe:

```text
Audeu.EplanAi.LegacyHost.v1
```

v0.1 has no TCP, HTTP, or web listener. No inbound firewall rule is required.

The Task 13 static security verifier fails CI if bridge source introduces listener/client primitives covered by the Gate D policy.

## No arbitrary model-controlled path

The approved eight MCP tools contain no generic filesystem path/file path parameter. Release/build scripts use operator-controlled workstation paths; those scripts are not MCP tools and are not model-callable through the bridge.

## No arbitrary action or operation forwarding

The AddIn/LegacyHost registry accepts fixed operations only. v0.1 does not accept model-provided EPLAN action names, generic bridge operation names, or arbitrary EPLAN property IDs.

Unknown operations fail closed.

## No arbitrary code execution

Bridge source does not expose model-controlled shell/process execution, PowerShell execution, dynamic assembly loading, or source-code execution. The Gate D verifier checks for the forbidden dynamic-code/process primitives covered by the release policy.

Build/release PowerShell scripts execute only as explicit operator actions outside the MCP surface.

## Change Plan lifetime

Change Plans are in-memory and owned by one MCP process. They are not persisted across process restart in v0.1. Restarting MCP intentionally discards them.

Stable plan errors such as `PLAN_NOT_FOUND`, `PLAN_STALE`, and `PLAN_ALREADY_APPLIED` are safety signals and should cause a fresh inspection/preview rather than payload modification.

## Packaging boundary

### AddIn

The AddIn archive contains project-owned binaries needed beside the AddIn. It intentionally does not redistribute EPLAN AF, Base, DataModel, or HEServices assemblies. Those are loaded from the verified EPLAN installation.

### LegacyHost

LegacyHost is packaged separately and may contain EPLAN RemoteClient/Remoting runtime assemblies copied by the build because those are private dependencies of the gateway process.

### MCP

MCP is published framework-dependent for `win-x64` and requires the x64 .NET 8 runtime. It carries no EPLAN assemblies.

### Integrity

All four ZIP archives are covered by `SHA256SUMS.txt`. The Task 13 builder recomputes and verifies every hash before it reports release PASS.

## Automated Gate D checks

Task 13 adds tests/guards that verify:

- exact release archive names and combined-bundle layout;
- checksum manifest coverage and sorted deterministic entry order;
- missing primary component binaries fail closed;
- network listener primitives are rejected by the security verifier;
- dynamic code/shell/process primitives covered by policy are rejected;
- generic MCP `propertyId`, action/operation/path/code-style inputs are rejected;
- the current real `src` tree passes the security verifier;
- a release build refuses an absent EPLAN API directory;
- release builder contains explicit EPLAN-running and LegacyHost-running guards;
- AddIn release staging rejects prohibited EPLAN platform DLL redistribution.

## Real-workstation acceptance inherited by v0.1

Gate B proved the complete read-only path through MCP -> named pipe -> LegacyHost -> EPLAN RemoteClient -> AddIn -> EPLAN, including stale-session behavior.

Gate C proved the complete approved write path on a disposable EPLAN 2.9.4 project copy:

- preview did not mutate EPLAN;
- apply changed exactly the previewed Function Text;
- read-back observed the applied value;
- undo restored the exact original text;
- a controlled pre-commit failure rolled back to the exact original text;
- LegacyHost/RemoteClient shut down cleanly.

Task 13 is packaging/documentation/verification only. `verify-task13-gate-d.ps1` checks that Task 13 has not modified runtime source under `src/`; if it has, it refuses Gate D and requires the applicable real Gate B/Gate C acceptance to be repeated.

## Deferred attack surface

The following are intentionally absent from v0.1:

- arbitrary EPLAN property/action access;
- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- network-accessible bridge service;
- arbitrary filesystem operations;
- arbitrary code/shell execution.

Any future feature that expands these boundaries requires a new explicit design/security review rather than reuse of the v0.1 write channel.

## Gate D status

Repo-side packaging and security verification can be completed in CI. Final Gate D PASS additionally requires a release build on the verified EPLAN 2.9.4 workstation using `scripts\verify-task13-gate-d.ps1` and recorded artifact hashes. Until that workstation evidence is recorded, Task 13 must not be tagged or released as final v0.1.