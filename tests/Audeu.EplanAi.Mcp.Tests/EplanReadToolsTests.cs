using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Tools;
using Audeu.EplanAi.Mcp.Transport;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class EplanReadToolsTests
{
    [Fact]
    public async Task Tools_MapExactlyToFiveClosedWorldReadOperations()
    {
        var bridge = new RecordingBridgeClient();
        Guid sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        await EplanReadTools.StatusAsync(bridge, CancellationToken.None);
        await EplanReadTools.GetActiveProjectAsync(bridge, CancellationToken.None);
        await EplanReadTools.ListPagesAsync(sessionId, bridge, CancellationToken.None);
        await EplanReadTools.GetSelectionAsync(sessionId, bridge, CancellationToken.None);
        await EplanReadTools.GetObjectAsync(
            sessionId,
            25,
            177,
            123456789,
            "===+++УСПД_Э3#-A1",
            bridge,
            CancellationToken.None);

        Assert.Equal(
            new[] { "system.status", "project.getActive", "project.listPages", "selection.get", "object.get" },
            bridge.Requests.Select(request => request.Operation));
        Assert.All(bridge.Requests, request => Assert.Equal("read", request.Mode));
        Assert.All(bridge.Requests, request => Assert.Equal(ProtocolConstants.Version, request.ProtocolVersion));
        Assert.Equal(5, bridge.Requests.Select(request => request.RequestId).Distinct().Count());
    }

    [Fact]
    public async Task SessionAndCompactObjectReference_AreForwardedWithoutRawEplanTypes()
    {
        var bridge = new RecordingBridgeClient();
        Guid sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        await EplanReadTools.GetObjectAsync(
            sessionId,
            uint.MaxValue,
            177,
            long.MaxValue,
            "===+++УСПД_Э3#-A1",
            bridge,
            CancellationToken.None);

        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal(sessionId, request.ProjectSessionId);
        Assert.Equal("4294967295", request.Arguments["databaseIdentifier"]);
        Assert.Equal("177", request.Arguments["typeIdentifier"]);
        Assert.Equal("9223372036854775807", request.Arguments["objectIdentifier"]);
        Assert.Equal("===+++УСПД_Э3#-A1", request.Arguments["stringIdentifier"]);
        Assert.Equal(4, request.Arguments.Count);
    }

    [Fact]
    public async Task Success_ReturnsCompactBridgeJsonUnchanged()
    {
        var bridge = new RecordingBridgeClient("{\"status\":\"ready\",\"protocolVersion\":\"1.0\"}");

        string result = await EplanReadTools.StatusAsync(bridge, CancellationToken.None);

        Assert.Equal("{\"status\":\"ready\",\"protocolVersion\":\"1.0\"}", result);
    }

    [Fact]
    public async Task BridgeFailure_IsSurfacedAsControlledMcpErrorWithStableCode()
    {
        var bridge = new RecordingBridgeClient(responseFactory: request => new BridgeResponse
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = request.RequestId,
            Success = false,
            ErrorCode = BridgeErrorCodes.SessionStale,
            ErrorMessage = "Project session is stale."
        });

        McpException error = await Assert.ThrowsAsync<McpException>(
            () => EplanReadTools.ListPagesAsync(Guid.NewGuid(), bridge, CancellationToken.None));

        Assert.StartsWith(BridgeErrorCodes.SessionStale + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCompactReference_IsRejectedBeforeTransport()
    {
        var bridge = new RecordingBridgeClient();

        McpException error = await Assert.ThrowsAsync<McpException>(
            () => EplanReadTools.GetObjectAsync(
                Guid.NewGuid(),
                25,
                177,
                123,
                "",
                bridge,
                CancellationToken.None));

        Assert.StartsWith(BridgeErrorCodes.ValidationFailed + ":", error.Message, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact]
    public void Metadata_ExposesExactlyFiveReadOnlyClosedWorldTools()
    {
        MethodInfo[] methods = typeof(EplanReadTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

        Assert.Equal(5, methods.Length);
        string[] names = methods
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(new[]
        {
            "eplan_get_active_project",
            "eplan_get_object",
            "eplan_get_selection",
            "eplan_list_pages",
            "eplan_status"
        }, names);

        foreach (MethodInfo method in methods)
        {
            McpServerToolAttribute attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
            Assert.True(attribute.ReadOnly);
            Assert.False(attribute.Destructive);
            Assert.True(attribute.Idempotent);
            Assert.False(attribute.OpenWorld);
        }
    }

    private sealed class RecordingBridgeClient : IBridgeClient
    {
        private readonly string _dataJson;
        private readonly Func<BridgeRequest, BridgeResponse> _responseFactory;

        public RecordingBridgeClient(
            string dataJson = "{}",
            Func<BridgeRequest, BridgeResponse> responseFactory = null)
        {
            _dataJson = dataJson;
            _responseFactory = responseFactory;
        }

        public List<BridgeRequest> Requests { get; } = new();

        public Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            BridgeResponse response = _responseFactory?.Invoke(request) ?? new BridgeResponse
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = request.RequestId,
                Success = true,
                DataJson = _dataJson
            };
            return Task.FromResult(response);
        }
    }
}