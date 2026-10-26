using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Transport;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class NamedPipeBridgeClientTests
{
    [Fact]
    public async Task SendAsync_RoundTripsThroughFixedLocalPipe()
    {
        var request = new BridgeRequest
        {
            RequestId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Operation = "system.status",
            Mode = "read"
        };

        await using var server = CreateServer();
        Task serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
            BridgeRequest received = await BridgePipeProtocol.ReadRequestAsync(server, CancellationToken.None);
            Assert.Equal(request.RequestId, received.RequestId);
            Assert.Equal("system.status", received.Operation);
            Assert.Equal("read", received.Mode);

            await BridgePipeProtocol.WriteResponseAsync(server, new BridgeResponse
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = received.RequestId,
                Success = true,
                DataJson = "{\"status\":\"ready\"}"
            }, CancellationToken.None);
        });

        var client = new NamedPipeBridgeClient(TimeSpan.FromSeconds(1));
        BridgeResponse response = await client.SendAsync(request, CancellationToken.None);
        await serverTask;

        Assert.True(response.Success);
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal("{\"status\":\"ready\"}", response.DataJson);
    }

    [Fact]
    public async Task SendAsync_RejectsMismatchedResponseRequestId()
    {
        var request = NewRequest();
        await using var server = CreateServer();
        Task serverTask = RespondAsync(server, new BridgeResponse
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = Guid.NewGuid(),
            Success = true,
            DataJson = "{}"
        });

        var client = new NamedPipeBridgeClient(TimeSpan.FromSeconds(1));
        BridgeResponse response = await client.SendAsync(request, CancellationToken.None);
        await serverTask;

        Assert.False(response.Success);
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal(BridgeErrorCodes.InternalError, response.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_MapsProtocolMismatchToStableBridgeError()
    {
        var request = NewRequest();
        await using var server = CreateServer();
        Task serverTask = RespondAsync(server, new BridgeResponse
        {
            ProtocolVersion = "9.9",
            RequestId = request.RequestId,
            Success = true,
            DataJson = "{}"
        });

        var client = new NamedPipeBridgeClient(TimeSpan.FromSeconds(1));
        BridgeResponse response = await client.SendAsync(request, CancellationToken.None);
        await serverTask;

        Assert.False(response.Success);
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal(BridgeErrorCodes.ProtocolMismatch, response.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_MapsConnectTimeoutToOperationTimeout()
    {
        var request = NewRequest();
        var client = new NamedPipeBridgeClient(TimeSpan.FromMilliseconds(25));

        BridgeResponse response = await client.SendAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal(BridgeErrorCodes.OperationTimeout, response.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_PropagatesCallerCancellation()
    {
        var request = NewRequest();
        var client = new NamedPipeBridgeClient(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cancellation.Token));
    }

    private static NamedPipeServerStream CreateServer() => new(
        BridgePipeProtocol.PipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);

    private static BridgeRequest NewRequest() => new()
    {
        RequestId = Guid.NewGuid(),
        Operation = "system.status",
        Mode = "read"
    };

    private static async Task RespondAsync(NamedPipeServerStream server, BridgeResponse response)
    {
        await server.WaitForConnectionAsync();
        await BridgePipeProtocol.ReadRequestAsync(server, CancellationToken.None);
        await BridgePipeProtocol.WriteResponseAsync(server, response, CancellationToken.None);
    }
}
