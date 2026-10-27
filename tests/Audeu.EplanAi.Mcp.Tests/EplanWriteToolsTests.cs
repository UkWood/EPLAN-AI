using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Tools;
using Audeu.EplanAi.Mcp.Transport;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class EplanWriteToolsTests
{
    [Fact]
    public void Metadata_ExposesExactlyThreeClosedWorldTask12Tools()
    {
        MethodInfo[] methods = typeof(EplanWriteTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

        Assert.Equal(3, methods.Length);
        Assert.Equal(
            new[]
            {
                "eplan_apply_change_plan",
                "eplan_preview_set_function_text",
                "eplan_undo_last_ai_change"
            },
            methods
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        MethodInfo preview = Method("eplan_preview_set_function_text", methods);
        AssertMetadata(preview, readOnly: true, destructive: false, idempotent: false);

        MethodInfo apply = Method("eplan_apply_change_plan", methods);
        AssertMetadata(apply, readOnly: false, destructive: true, idempotent: false);

        MethodInfo undo = Method("eplan_undo_last_ai_change", methods);
        AssertMetadata(undo, readOnly: false, destructive: true, idempotent: false);
    }

    [Fact]
    public void ApplyTool_ModelSchemaContainsOnlyChangePlanId()
    {
        MethodInfo apply = typeof(EplanWriteTools).GetMethod(nameof(EplanWriteTools.ApplyChangePlanAsync))!;
        ParameterInfo[] modelParameters = apply.GetParameters()
            .Where(parameter =>
                parameter.ParameterType != typeof(ChangePlanApplyService)
                && parameter.ParameterType != typeof(CancellationToken))
            .ToArray();

        ParameterInfo parameter = Assert.Single(modelParameters);
        Assert.Equal("changePlanId", parameter.Name);
        Assert.Equal(typeof(Guid), parameter.ParameterType);
    }

    [Fact]
    public void UndoTool_ModelSchemaContainsOnlyProjectSessionId()
    {
        MethodInfo undo = typeof(EplanWriteTools).GetMethod(nameof(EplanWriteTools.UndoLastAiChangeAsync))!;
        ParameterInfo[] modelParameters = undo.GetParameters()
            .Where(parameter =>
                parameter.ParameterType != typeof(IBridgeClient)
                && parameter.ParameterType != typeof(CancellationToken))
            .ToArray();

        ParameterInfo parameter = Assert.Single(modelParameters);
        Assert.Equal("projectSessionId", parameter.Name);
        Assert.Equal(typeof(Guid), parameter.ParameterType);
    }

    [Fact]
    public async Task Preview_IsReadOnlyAndReturnsStoredPlanJson()
    {
        var bridge = new RecordingBridge(request => Success(
            request,
            "{\"kind\":\"Function\",\"functionText\":\"Old text\"}"));
        var store = new ChangePlanStore();
        var planner = new FunctionTextChangePlanner(bridge, store, TimeProvider.System);
        Guid sessionId = Guid.NewGuid();

        string json = await EplanWriteTools.PreviewSetFunctionTextAsync(
            sessionId,
            25,
            177,
            123456789,
            "===+++TEST#-A1",
            "New text",
            planner,
            CancellationToken.None);

        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal("object.get", request.Operation);
        Assert.Equal("read", request.Mode);
        Assert.Equal(sessionId, request.ProjectSessionId);
        Assert.Contains("\"changePlanId\":", json, StringComparison.Ordinal);
        Assert.Contains("\"projectSessionId\":\"" + sessionId.ToString("D") + "\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"previewed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"setFunctionText\"", json, StringComparison.Ordinal);
        Assert.Contains("\"oldValue\":\"Old text\"", json, StringComparison.Ordinal);
        Assert.Contains("\"newValue\":\"New text\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_UsesStoredPlanServiceAndReturnsAppliedPlanJson()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan();
        store.Add(plan);
        var bridge = new RecordingBridge(request => Success(request, "{\"status\":\"applied\"}"));
        var applyService = new ChangePlanApplyService(store, bridge);

        string json = await EplanWriteTools.ApplyChangePlanAsync(
            plan.ChangePlanId,
            applyService,
            CancellationToken.None);

        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal("change.apply", request.Operation);
        Assert.Equal("write", request.Mode);
        Assert.Equal(plan.ChangePlanId.ToString("D"), request.Arguments["changePlanId"]);
        Assert.Contains("\"status\":\"applied\"", json, StringComparison.Ordinal);
        Assert.Contains("\"changePlanId\":\"" + plan.ChangePlanId.ToString("D") + "\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Undo_UsesFixedOperationCurrentSessionAndNoCallerPayload()
    {
        var bridge = new RecordingBridge(request => Success(request, "{\"status\":\"undone\"}"));
        Guid sessionId = Guid.NewGuid();

        string json = await EplanWriteTools.UndoLastAiChangeAsync(
            sessionId,
            bridge,
            CancellationToken.None);

        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal("change.undoLast", request.Operation);
        Assert.Equal("write", request.Mode);
        Assert.Equal(sessionId, request.ProjectSessionId);
        Assert.Empty(request.Arguments);
        Assert.Equal("{\"status\":\"undone\"}", json);
    }

    [Fact]
    public async Task ChangePlanFailure_IsSurfacedAsControlledMcpError()
    {
        var service = new ChangePlanApplyService(new ChangePlanStore(), new RecordingBridge());

        McpException error = await Assert.ThrowsAsync<McpException>(
            () => EplanWriteTools.ApplyChangePlanAsync(
                Guid.NewGuid(),
                service,
                CancellationToken.None));

        Assert.StartsWith(BridgeErrorCodes.PlanNotFound + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndoUnknownBridgeError_FailsClosed()
    {
        var bridge = new RecordingBridge(request => new BridgeResponse
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = request.RequestId,
            Success = false,
            ErrorCode = "NEW_UNKNOWN_CODE",
            ErrorMessage = "Unexpected."
        });

        McpException error = await Assert.ThrowsAsync<McpException>(
            () => EplanWriteTools.UndoLastAiChangeAsync(
                Guid.NewGuid(),
                bridge,
                CancellationToken.None));

        Assert.StartsWith(BridgeErrorCodes.InternalError + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyUndoSession_IsRejectedBeforeTransport()
    {
        var bridge = new RecordingBridge();

        McpException error = await Assert.ThrowsAsync<McpException>(
            () => EplanWriteTools.UndoLastAiChangeAsync(
                Guid.Empty,
                bridge,
                CancellationToken.None));

        Assert.StartsWith(BridgeErrorCodes.ValidationFailed + ":", error.Message, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    private static MethodInfo Method(string name, IEnumerable<MethodInfo> methods) =>
        methods.Single(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name == name);

    private static void AssertMetadata(
        MethodInfo method,
        bool readOnly,
        bool destructive,
        bool idempotent)
    {
        McpServerToolAttribute attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
        Assert.Equal(readOnly, attribute.ReadOnly);
        Assert.Equal(destructive, attribute.Destructive);
        Assert.Equal(idempotent, attribute.Idempotent);
        Assert.False(attribute.OpenWorld);
    }

    private static ChangePlan CreatePlan()
    {
        Guid session = Guid.NewGuid();
        return new ChangePlan(
            Guid.NewGuid(),
            DateTime.UtcNow,
            session,
            new[]
            {
                new ChangeOperation(
                    "setFunctionText",
                    new ObjectReference
                    {
                        ProjectSessionId = session,
                        DatabaseIdentifier = 25,
                        TypeIdentifier = 177,
                        ObjectIdentifier = 123456789,
                        StringIdentifier = "===+++TEST#-A1"
                    },
                    "Old text",
                    "New text")
            });
    }

    private static BridgeResponse Success(BridgeRequest request, string dataJson) => new()
    {
        ProtocolVersion = ProtocolConstants.Version,
        RequestId = request.RequestId,
        Success = true,
        DataJson = dataJson
    };

    private sealed class RecordingBridge : IBridgeClient
    {
        private readonly Func<BridgeRequest, BridgeResponse> _responseFactory;

        public RecordingBridge(Func<BridgeRequest, BridgeResponse> responseFactory = null)
        {
            _responseFactory = responseFactory ?? (request => Success(request, "{}"));
        }

        public List<BridgeRequest> Requests { get; } = new();

        public Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }
}
