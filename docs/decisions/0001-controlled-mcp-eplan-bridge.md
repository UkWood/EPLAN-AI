# ADR-0001: Use a controlled MCP bridge instead of direct LLM-to-EPLAN API access

**Status:** Accepted  
**Date:** 2026-10-23

## Context

The project must let an AI assistant work with EPLAN Platform 2.9 projects. EPLAN exposes a rich .NET API, Actions, DataModel/HEServices, Remoting, transactions, undo and validation mechanisms. A language model could theoretically be exposed to a very large low-level API surface, but doing so would make project mutation difficult to validate, reproduce and secure.

EPLAN 2.9 is also tied to .NET Framework 4.7.2-era API assemblies, while the AI/MCP layer should use a current runtime and remain replaceable independently of the EPLAN version.

## Decision

Use a three-process controlled bridge:

```text
AI / MCP client
    -> Audeu.EplanAi.Mcp (.NET 8)
    -> local Named Pipe
    -> Audeu.EplanAi.LegacyHost (.NET Framework 4.7.2)
    -> EPLAN Remoting / WCF
    -> Audeu.EplanAi.AddIn (.NET Framework 4.7.2)
    -> EPLAN 2.9 DataModel / HEServices
```

The EPLAN add-in exposes one fixed dispatch Action: `AudeuAiDispatch`.

The LLM is not allowed to supply arbitrary:
- EPLAN Action names;
- EPLAN property IDs;
- C# code;
- DLL paths;
- filesystem paths;
- direct DataModel method names.

Large request/response payloads travel through a local GUID-keyed spool. EPLAN Remoting carries only the fixed dispatch invocation and request identifier.

All mutating operations use an explicit immutable Change Plan and are designed around Preview → Apply → Verify → rollback/undo.

## Consequences

### Positive
- EPLAN-specific runtime is isolated from the modern MCP process.
- The AI-visible tool surface can remain small and domain-oriented.
- Validation and engineering rules can be deterministic.
- Changes can be audited and reproduced.
- EPLAN versions can later be adapted behind the bridge without redesigning the AI interface.
- The architecture avoids fragile mouse/keyboard automation as the primary control path.

### Negative
- More components must be built and deployed.
- Local IPC and process lifecycle must be tested.
- EPLAN API licensing/runtime prerequisites must be validated early.
- The first useful vertical slice requires integration testing on a real EPLAN 2.9 workstation.

## Rejected alternatives

### Direct LLM → full EPLAN API
Rejected because it exposes too large and unsafe a mutation surface, leaks EPLAN implementation detail into prompts/tool schemas, and makes validation difficult.

### GUI / computer-vision automation as the primary mechanism
Rejected because it is less deterministic, more fragile across layout/state changes, harder to audit and cannot leverage DataModel transactions/undo/validation directly.

### EPLAN scripts only
Rejected as the long-term architecture because scripts are useful for limited Actions/settings automation but do not provide the same maintainable, extensible DataModel integration as the approved API/add-in route.

### HTTP server inside EPLAN
Rejected for v0.1 because it introduces network exposure and threading/lifecycle complexity inside the EPLAN process. Local Remoting plus a current-user IPC bridge is safer and easier to constrain.

## Follow-up

The next mandatory decision gate is environmental: verify EPLAN 2.9 API/API Extension availability and assembly paths on the target workstation before substantial EPLAN-dependent implementation begins.
