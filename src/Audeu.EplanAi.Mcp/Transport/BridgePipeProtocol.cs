using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.Mcp.Transport;

public static class BridgePipeProtocol
{
    public const string PipeName = "Audeu.EplanAi.LegacyHost.v1";
    public const int MaxRequestBytes = 1024 * 1024;
    public const int MaxResponseBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Task WriteRequestAsync(Stream stream, BridgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WriteFrameAsync(
            stream,
            JsonSerializer.SerializeToUtf8Bytes(PipeBridgeRequest.FromContract(request), JsonOptions),
            MaxRequestBytes,
            cancellationToken);
    }

    public static async Task<BridgeRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] payload = await ReadFrameAsync(stream, MaxRequestBytes, cancellationToken).ConfigureAwait(false);
        PipeBridgeRequest value = Deserialize<PipeBridgeRequest>(payload);
        return value.ToContract();
    }

    public static Task WriteResponseAsync(Stream stream, BridgeResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        return WriteFrameAsync(
            stream,
            JsonSerializer.SerializeToUtf8Bytes(PipeBridgeResponse.FromContract(response), JsonOptions),
            MaxResponseBytes,
            cancellationToken);
    }

    public static async Task<BridgeResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] payload = await ReadFrameAsync(stream, MaxResponseBytes, cancellationToken).ConfigureAwait(false);
        PipeBridgeResponse value = Deserialize<PipeBridgeResponse>(payload);
        return value.ToContract();
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        byte[] payload,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length <= 0 || payload.Length > maxBytes)
        {
            throw new InvalidDataException("Pipe payload length is outside the allowed range.");
        }

        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadFrameAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] prefix = new byte[4];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);

        int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length <= 0 || length > maxBytes)
        {
            throw new InvalidDataException("Pipe payload length is outside the allowed range.");
        }

        byte[] payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Pipe message ended before the declared frame length was read.");
            }

            offset += read;
        }
    }

    private static T Deserialize<T>(byte[] payload)
    {
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Pipe payload contains invalid JSON.", ex);
        }

        return value ?? throw new InvalidDataException("Pipe payload contains no JSON value.");
    }

    private sealed class PipeBridgeRequest
    {
        public string? ProtocolVersion { get; set; }
        public Guid RequestId { get; set; }
        public string? Operation { get; set; }
        public string? Mode { get; set; }
        public Guid? ProjectSessionId { get; set; }
        public Dictionary<string, string>? Arguments { get; set; }

        public static PipeBridgeRequest FromContract(BridgeRequest request) => new()
        {
            ProtocolVersion = request.ProtocolVersion,
            RequestId = request.RequestId,
            Operation = request.Operation,
            Mode = request.Mode,
            ProjectSessionId = request.ProjectSessionId,
            Arguments = request.Arguments ?? new Dictionary<string, string>()
        };

        public BridgeRequest ToContract() => new()
        {
            ProtocolVersion = ProtocolVersion,
            RequestId = RequestId,
            Operation = Operation,
            Mode = Mode,
            ProjectSessionId = ProjectSessionId,
            Arguments = Arguments ?? new Dictionary<string, string>()
        };
    }

    private sealed class PipeBridgeResponse
    {
        public string? ProtocolVersion { get; set; }
        public Guid RequestId { get; set; }
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DataJson { get; set; }
        public string[]? Warnings { get; set; }

        public static PipeBridgeResponse FromContract(BridgeResponse response) => new()
        {
            ProtocolVersion = response.ProtocolVersion,
            RequestId = response.RequestId,
            Success = response.Success,
            ErrorCode = response.ErrorCode,
            ErrorMessage = response.ErrorMessage,
            DataJson = response.DataJson,
            Warnings = response.Warnings ?? Array.Empty<string>()
        };

        public BridgeResponse ToContract() => new()
        {
            ProtocolVersion = ProtocolVersion,
            RequestId = RequestId,
            Success = Success,
            ErrorCode = ErrorCode,
            ErrorMessage = ErrorMessage,
            DataJson = DataJson,
            Warnings = Warnings ?? Array.Empty<string>()
        };
    }
}
