# EPLAN AI Bridge v0.1.0 — Release Notes

## Release scope

EPLAN AI Bridge v0.1.0 is the first controlled MCP-to-EPLAN bridge release. It provides safe inspection of the active EPLAN project plus one constrained Function Text write workflow.

Verified target:

- EPLAN Electric P8 Professional 64-bit;
- 2.9 SP1 Update 4, build 14773;
- Windows x64;
- EPLAN API/API Extension;
- x64 .NET 8 runtime for the MCP process.

## Architecture

```text
MCP client / ChatGPT
  -> stdio
Audeu.EplanAi.Mcp (.NET 8)
  -> fixed current-user named pipe
Audeu.EplanAi.LegacyHost (.NET Framework 4.7.2)
  -> EPLAN RemoteClient / AudeuAiDispatch
Audeu.EplanAi.AddIn (.NET Framework 4.7.2)
  -> EPLAN DataModel / HEServices
```

The MCP process does not reference EPLAN assemblies. LegacyHost is the only RemoteClient process. AddIn is the only DataModel/HEServices boundary.

## Model-callable tools

v0.1 exposes exactly eight MCP tools:

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

## Read capabilities

- bridge status and protocol state;
- active project information and project session identity;
- page list;
- current selection;
- Function/object inspection;
- displayed multilingual Function Text normalization;
- stale project-session rejection.

## Safe Function Text workflow

The one v0.1 write workflow is:

```text
inspect Function
  -> preview proposed Function Text
  -> immutable Change Plan
  -> apply by changePlanId only
  -> read-back verification
  -> guarded undo or rollback
```

Safety properties include:

- immutable target/old/new preview state;
- single-use plan claim;
- current project/session validation;
- exact Function identity revalidation;
- exact old-value comparison before mutation;
- only `FUNC_TEXT` can be changed;
- EPLAN UndoStep + SafetyPoint + Transaction boundary;
- exact displayed-text read-back before commit;
- fail-closed rollback on controlled failure or post-condition mismatch;
- current-session guarded last-AI-change undo.

## Acceptance evidence

### Gate B — read-only path

Passed on the real EPLAN 2.9.4 workstation. The acceptance covered:

- exact read-only tool discovery;
- status and active project;
- page list;
- selected Function inspection;
- exact displayed Function Text;
- `NO_ACTIVE_PROJECT` after project close;
- fresh session after reopen;
- stale old Function reference rejected with `SESSION_STALE`;
- clean LegacyHost shutdown.

### Gate C — safe write path

Passed on disposable project copy `УСПД Жуковский ПНС Gate C` with Function `===+++УСПД_Э3#-A1`.

The acceptance proved:

- preview did not mutate EPLAN;
- apply changed exactly the previewed Function Text;
- read-back returned the applied value;
- undo restored the exact original value;
- an injected controlled pre-commit failure rolled back to the exact original value;
- the complete MCP -> named pipe -> LegacyHost -> RemoteClient -> AddIn -> EPLAN path worked;
- graceful LegacyHost/RemoteClient shutdown.

### Gate D — release/package verification

Gate D covers final non-EPLAN tests, security verification, real EPLAN 2.9.4 compile, release packaging, and SHA-256 verification. It becomes PASS only after `scripts\verify-task13-gate-d.ps1` succeeds on the verified workstation and the resulting archive hashes are recorded.

## Release artifacts

The v0.1 builder produces:

```text
Audeu.EplanAi.AddIn-0.1.0.zip
Audeu.EplanAi.LegacyHost-0.1.0.zip
Audeu.EplanAi.Mcp-0.1.0-win-x64.zip
EplanAiBridge-v0.1.0.zip
SHA256SUMS.txt
```

The combined archive includes installation, troubleshooting, and security documents.

## Security boundary

v0.1 does not expose:

- arbitrary EPLAN action forwarding;
- arbitrary property ID input;
- arbitrary bridge operation names;
- model-controlled arbitrary filesystem paths;
- TCP/HTTP listener;
- shell/process or dynamic code execution;
- generic write APIs.

Apply accepts only a `changePlanId` created by preview. Undo accepts only the current `projectSessionId`.

See `SECURITY.md` for the full Gate D security review.

## Known operational characteristics

- Change Plans live only in the MCP process and are lost when MCP restarts.
- Project/object references are session-scoped; closing/reopening or switching the project invalidates old references.
- LegacyHost is a persistent console process and must remain running during use.
- The MCP release is framework-dependent and requires an x64 .NET 8 runtime.
- The AddIn package intentionally does not redistribute core EPLAN API assemblies.
- v0.1 is validated against one exact EPLAN 2.9.4 target; other versions require separate compatibility verification.

## Deferred from v0.1

- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- arbitrary EPLAN property/action access;
- network service deployment;
- MSI/MSIX/Setup.exe installer.

## Upgrade direction

Future releases should expand the closed-world domain API incrementally. New write targets must receive their own immutable preview/apply/verify/undo policy and real-workstation acceptance rather than being routed through a generic EPLAN action/property interface.

## Planned tag

After Task 13 is merged, final `main` CI is green, and explicit release approval is given, the planned tag is:

```text
eplan-ai-bridge-v0.1.0
```

The tag must point to the final verified `main` release commit.