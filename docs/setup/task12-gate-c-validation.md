# Task 12 / Gate C workstation validation

Gate C is the first real write acceptance for EPLAN AI Bridge. Run it **only on a disposable/safe copy of an EPLAN project**. Do not use a production project or the normal engineering working copy.

The acceptance sequence intentionally changes the Function Text of one selected Function, verifies the change, undoes it, and then injects a controlled pre-commit failure to prove transaction rollback.

## Preconditions

- Windows workstation with EPLAN Electric P8 2.9.4 running normally.
- Repository branch `m1/task-12-gate-c` checked out.
- A disposable/safe project copy whose exact project name is known.
- Exactly one writable `Function` selected in EPLAN.
- The selected Function must belong to the active disposable project.
- Two PowerShell windows opened in the repository root.

The Gate C script requires the disposable project name twice. Both values must match each other exactly and must also match the active project name returned by EPLAN before any write is attempted.

## 1. Build with EPLAN closed

Close EPLAN completely and stop any running LegacyHost. Then run:

```powershell
.\scripts\verify-task9-build.ps1
```

This rebuilds the AddIn against the real EPLAN 2.9.4 API, the LegacyHost, MCP server and the official MCP probe. The Task 12 code is contained in those same artifacts.

After the build passes, start EPLAN again, open only the disposable Gate C project and select exactly one writable Function.

## 2. Window 1 — start persistent LegacyHost

```powershell
.\scripts\run-task7-legacyhost.ps1
```

Leave this window open for the entire Gate C run.

## 3. Window 2 — run Gate C

Replace `Gate C Scratch` with the exact EPLAN project name of your disposable copy. The same name is deliberately supplied twice:

```powershell
.\scripts\test-task12-gate-c.ps1 `
  -ExpectedProjectName 'Gate C Scratch' `
  -ConfirmedDisposableProjectName 'Gate C Scratch'
```

You may override the temporary text if needed:

```powershell
.\scripts\test-task12-gate-c.ps1 `
  -ExpectedProjectName 'Gate C Scratch' `
  -ConfirmedDisposableProjectName 'Gate C Scratch' `
  -TemporaryFunctionText 'Audeu Gate C temporary Function Text'
```

The script refuses to run if the two project-name confirmations differ, if either is empty, if EPLAN is not running, or if the temporary Function Text is empty or longer than 1024 UTF-16 code units.

## Expected acceptance sequence

A successful run prints PASS lines for these checks:

1. MCP discovery exposes exactly eight approved Task 12 tools.
2. `eplan_status` reports ready.
3. The active project exactly matches both disposable-project confirmations.
4. Exactly one selected object is a Function and its original displayed Function Text is captured.
5. `eplan_preview_set_function_text` returns one immutable `setFunctionText` plan containing the exact target, old value and proposed new value.
6. A read immediately after preview proves that preview did not change EPLAN.
7. `eplan_apply_change_plan` receives only the `changePlanId`; read-back proves the previewed text was applied.
8. `eplan_undo_last_ai_change` receives only the current `projectSessionId`; read-back proves the exact original text was restored.
9. A second preview is created for the rollback test.
10. The probe creates the fixed one-shot marker `%TEMP%\Audeu.EplanAi.GateC.fail-next-write` immediately before apply.
11. The AddIn consumes that marker after assigning Function Text but before commit and deliberately returns `EPLAN_ACTION_FAILED`.
12. Read-back proves that EPLAN transaction rollback restored the exact original Function Text.
13. The marker is removed even if the probe fails.

The final line should be:

```text
PASS: Gate C real MCP-to-EPLAN preview/apply/undo/rollback acceptance completed successfully.
```

## Safety properties being proven

Gate C does not expose arbitrary EPLAN write access. The model-callable write surface remains fixed to preview, apply-by-plan-ID and undo-last-AI-change. Apply cannot replace the previewed target or values because the MCP tool accepts only a `changePlanId`.

The controlled failure mechanism is test-only and fixed. It is not an MCP parameter and is not model-callable. The AddIn accepts either the fixed environment variable `AUDEU_EPLAN_AI_GATE_C_FAIL_NEXT_WRITE=1` or the fixed same-user marker file `%TEMP%\Audeu.EplanAi.GateC.fail-next-write`; both are one-shot triggers consumed before the deliberate exception. The workstation probe uses the marker file so EPLAN does not need to be restarted between the successful apply/undo phase and the rollback phase.

## After the run

Return to Window 1 and press `Ctrl+C`. LegacyHost should stop cleanly.

Do not merge Task 12 solely because repository CI is green. Gate C is complete only after the real workstation run above passes on the disposable project and the observed PASS output is recorded in the Task 12 issue / PR evidence.
