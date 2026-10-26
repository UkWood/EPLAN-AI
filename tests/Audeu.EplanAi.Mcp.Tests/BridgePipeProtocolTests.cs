using System.Buffers.Binary;
using System.Text;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Transport;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class BridgePipeProtocolTests
{
    [Fact]
    public async Task WriteRequestAsync_UsesTask7FrameAndCanonicalCamelCaseJson()
    {
        var requestId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var request = new BridgeRequest
        {
            RequestId = requestId,
            Operation = "object.get",
            Mode = "read",
            ProjectSessionId = sessionId,
            Arguments = new Dictionary<string, string>
            {
                ["databaseIdentifier"] = "25"
            }
        };

        await using var stream = new MemoryStream();
        await BridgePipeProtocol.WriteRequestAsync(stream, request, CancellationToken.None);

        byte[] frame = stream.ToArray();
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(0, 4));
        Assert.Equal(frame.Length - 4, payloadLength);

        string json = Encoding.UTF8.GetString(frame, 4, payloadLength);
        Assert.Contains("\"protocolVersion\":\"1.0\"", json);
        Assert.Contains("\"requestId\":\"11111111-2222-3333-4444-555555555555\"", json);
        Assert.Contains("\"operation\":\"object.get\"", json);
        Assert.Contains("\"mode\":\"read\"", json);
        Assert.Contains("\"projectSessionId\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", json);
        Assert.Contains("\"arguments\":{\"databaseIdentifier\":\"25\"}", json);
        Assert.DoesNotContain("ProtocolVersion", json);
    }

    [Fact]
    public async Task ReadResponseAsync_DecodesTask7CanonicalResponse()
    {
        const string json = "{\"protocolVersion\":\"1.0\",\"requestId\":\"11111111-2222-3333-4444-555555555555\",\"success\":true,\"dataJson\":\"{\\\"status\\\":\\\"ready\\\"}\",\"warnings\":[]}";
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), payload.Length);
        payload.CopyTo(frame.AsSpan(4));

        await using var stream = new MemoryStream(frame, writable: false);
        BridgeResponse response = await BridgePipeProtocol.ReadResponseAsync(stream, CancellationToken.None);

        Assert.Equal(ProtocolConstants.Version, response.ProtocolVersion);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), response.RequestId);
        Assert.True(response.Success);
        Assert.Equal("{\"status\":\"ready\"}", response.DataJson);
        Assert.Empty(response.Warnings);
    }

    [Fact]
    public async Task WriteRequestAsync_RejectsPayloadLargerThanOneMiB()
    {
        var request = new BridgeRequest
        {
            RequestId = Guid.NewGuid(),
            Operation = "system.status",
            Mode = "read",
            Arguments = new Dictionary<string, string>
            {
                ["oversized"] = new string('x', BridgePipeProtocol.MaxRequestBytes + 1)
            }
        };

        await using var stream = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(
            () => BridgePipeProtocol.WriteRequestAsync(stream, request, CancellationToken.None));
    }

    [Fact]
    public async Task ReadResponseAsync_RejectsTruncatedFrame()
    {
        byte[] frame = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), 10);
        frame[4] = (byte)'{';
        frame[5] = (byte)'}';

        await using var stream = new MemoryStream(frame, writable: false);
        await Assert.ThrowsAsync<EndOfStreamException>(
            () => BridgePipeProtocol.ReadResponseAsync(stream, CancellationToken.None));
    }
}