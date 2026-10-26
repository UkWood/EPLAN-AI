# Task 7 Current-User Named-Pipe LegacyHost Design

**Status:** approved by existing Task 7 requirements and user instruction to continue  
**Date:** 2026-10-26

## Goal

Expose the completed persistent `RemoteGateway` only through a fixed local Windows named pipe so the future .NET 8 MCP process never references EPLAN assemblies or EPLAN Remoting directly.

## Fixed boundary

- pipe name: `Audeu.EplanAi.LegacyHost.v1`;
- local Windows named pipe only; no TCP/HTTP;
- server ACL permits only the current Windows user SID;
- no caller-controlled pipe name, host, port, EPLAN action name or filesystem path;
- maximum request payload: 1 MiB;
- one request per pipe connection;
- one EPLAN request in flight at a time;
- concurrent clients are serialized by a single server accept/process loop;
- LegacyHost owns one persistent `RemoteGateway` / `EplanRemoteClientAdapter` lifetime and disconnects on shutdown.

## Framing

Each message is:

```text
uint32 little-endian payloadLength
payloadLength bytes UTF-8 JSON
```

The server rejects zero-length and payloads over 1 MiB before allocation/deserialization. Responses use the same framing. `BridgeRequest` and `BridgeResponse` remain the only transport DTOs.

## Components

- `PipeProtocol`: fixed name, size limit, deterministic frame read/write and JSON serialization.
- `CurrentUserPipeSecurity`: creates `PipeSecurity` owned by/current-user SID with an allow rule for that SID only.
- `LegacyHostPipeServer`: creates a single secured server instance, accepts one client, reads one request, invokes an injected request executor, writes one response, disconnects, repeats.
- `IRemoteBridgeExecutor`: narrow async adapter over the existing `RemoteGateway` used by the server and replaced by fakes in tests.
- `PersistentLegacyHost`: production composition root that owns adapter/gateway/server and gracefully disconnects on exit.

## Error behavior

Malformed framing/JSON and oversize payloads never reach EPLAN. The pipe connection is terminated; where a valid request ID can be recovered, the server returns a stable bridge failure, otherwise transport failure closes the client connection. RemoteGateway bridge errors are passed through unchanged.

## Acceptance

CI must prove frame round-trip, partial reads, size rejection, current-user ACL construction, one-at-a-time request execution and shutdown disposal/disconnect behavior. Real workstation acceptance must prove a client can send `system.status` and read-only operations through the pipe to the already-running EPLAN without directly constructing `EplanRemoteClient`.