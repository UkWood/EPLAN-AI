# EPLAN AI

AI integration layer for **EPLAN Platform 2.9**.

The project goal is to build a safe, controlled bridge that lets an AI assistant inspect EPLAN projects and, later, create and modify engineering content through the official EPLAN API rather than GUI automation.

## Target architecture

```text
ChatGPT / MCP client
        |
        v
Audeu.EplanAi.Mcp            (.NET 8)
        |
        v
Audeu.EplanAi.LegacyHost     (.NET Framework 4.7.2)
        |
        v
EPLAN Remoting / WCF
        |
        v
Audeu.EplanAi.AddIn          (.NET Framework 4.7.2)
        |
        v
EPLAN 2.9 DataModel / HEServices
```

The LLM is intentionally separated from low-level EPLAN API calls. Engineering intent is converted into validated, deterministic operations before anything is applied to an EPLAN project.

## Current milestone

**EPLAN AI Bridge 0.1**

First prove a complete read-only path on a real EPLAN 2.9 installation, then add one tightly controlled write operation:

1. connect to EPLAN;
2. identify the active project;
3. list pages;
4. inspect the current selection;
5. inspect a selected `Function`;
6. preview a Function Text change;
7. apply it only after confirmation;
8. verify the result;
9. rollback/undo on failure.

Automatic schematic generation, macros, connections, device creation and equipment selection are intentionally deferred until this vertical slice is proven.

## Repository as project memory

This repository is the source of truth for the project. Architecture, plans, decisions, current status, implementation and test evidence are kept here so work can resume without relying on chat history.

Key documentation lives under `docs/`:

- `docs/design/` — approved technical designs;
- `docs/plans/` — implementation plans;
- `docs/PROJECT_STATE.md` — living project state and next action;
- `docs/decisions/` — durable architecture decisions.

## Development rule

No write capability is added until the read-only M0 acceptance gate works against a real EPLAN 2.9 instance.
