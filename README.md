# EPLAN AI Bridge

Controlled AI integration layer for **EPLAN Platform 2.9**.

EPLAN AI Bridge lets an MCP client inspect the active EPLAN project and perform one narrowly defined, reversible write workflow through the official EPLAN API rather than GUI automation or unrestricted API access.

## v0.1 architecture

```text
ChatGPT / MCP client
        |
        | MCP stdio
        v
Audeu.EplanAi.Mcp            (.NET 8)
        |
        | fixed current-user named pipe
        v
Audeu.EplanAi.LegacyHost     (.NET Framework 4.7.2)
        |
        | EPLAN RemoteClient / fixed AudeuAiDispatch
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

The MCP process references no EPLAN assembly. LegacyHost is the only process that owns EPLAN `RemoteClient`. AddIn is the only component that accesses EPLAN DataModel/HEServices.

## v0.1 MCP surface

Exactly eight model-callable tools are exposed.

Read-only:

- `eplan_status`
- `eplan_get_active_project`
- `eplan_list_pages`
- `eplan_get_selection`
- `eplan_get_object`

Safe Function Text workflow:

- `eplan_preview_set_function_text`
- `eplan_apply_change_plan`
- `eplan_undo_last_ai_change`

The write path is deliberately closed-world. Preview captures an immutable Function target plus exact old/new text. Apply accepts only the server-owned `changePlanId`, revalidates the current EPLAN session/object/value, writes only `FUNC_TEXT`, verifies the displayed result before commit, and is protected by EPLAN UndoStep/SafetyPoint/Transaction behavior. Undo is guarded by the current project session.

There is no generic set-property tool, arbitrary EPLAN action forwarding, arbitrary bridge operation, model-controlled filesystem path, network listener, or model-controlled shell/code execution.

## Verified target

The v0.1 acceptance target is:

- EPLAN Electric P8 Professional 64-bit;
- 2.9 SP1 Update 4, build 14773;
- EPLAN API/API Extension;
- Windows x64;
- x64 .NET 8 runtime for the MCP server.

Gate B passed the real read-only MCP-to-EPLAN path. Gate C passed preview -> apply -> verify -> undo plus controlled rollback on a disposable project copy. Task 13 / Gate D packages and verifies release `0.1.0` without changing runtime source.

## Release packaging

Task 13 produces:

```text
Audeu.EplanAi.AddIn-0.1.0.zip
Audeu.EplanAi.LegacyHost-0.1.0.zip
Audeu.EplanAi.Mcp-0.1.0-win-x64.zip
EplanAiBridge-v0.1.0.zip
SHA256SUMS.txt
```

Release documentation:

- `docs/release/v0.1/INSTALLATION.md`
- `docs/release/v0.1/TROUBLESHOOTING.md`
- `docs/release/v0.1/SECURITY.md`
- `docs/release/v0.1/RELEASE_NOTES.md`

The final workstation packaging gate is:

```powershell
.\scripts\verify-task13-gate-d.ps1
```

## Repository as project memory

GitHub is the durable source of truth for architecture, plans, decisions, implementation state and acceptance evidence.

Key locations:

- `docs/PROJECT_STATE.md` — current restart point and gate status;
- `docs/design/` and `docs/superpowers/specs/` — approved designs;
- `docs/plans/` and `docs/superpowers/plans/` — implementation plans;
- `docs/decisions/` — durable architecture decisions;
- `docs/setup/` and `docs/release/` — workstation/release procedures.

## Deferred beyond v0.1

- automatic schematic generation;
- macro insertion;
- connection creation;
- page creation/deletion;
- device/article selection;
- engineering rules engine;
- arbitrary EPLAN property/action access;
- network service deployment;
- MSI/MSIX/Setup.exe installer.

Future write capabilities must extend the closed-world domain API with their own preview/apply/verify/undo policy and real-workstation acceptance; they must not be routed through a generic EPLAN API escape hatch.
