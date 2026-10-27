using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Transport;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class FunctionTextChangePlannerTests
{
    [Fact]
    public async Task Preview_UsesExactlyOneReadOnlyObjectGetAndStoresExactValues()
    {
        var bridge = new RecordingBridgeClient("{\"kind\":\"Function\",\"functionText\":\"Old displayed text\"}");
        var store = new ChangePlanStore();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 30, 0, TimeSpan.Zero));
        var planner = new FunctionTextChangePlanner(bridge, store, clock);
        ObjectReference target = CreateTarget();

        ChangePlan plan = await planner.PreviewAsync(target, "New text", CancellationToken.None);

        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal(ProtocolConstants.Version, request.ProtocolVersion);
        Assert.NotEqual(Guid.Empty, request.RequestId);
        Assert.Equal("object.get", request.Operation);
        Assert.Equal("read", request.Mode);
        Assert.Equal(target.ProjectSessionId, request.ProjectSessionId);
        Assert.Equal("25", request.Arguments["databaseIdentifier"]);
        Assert.Equal("177", request.Arguments["typeIdentifier"]);
        Assert.Equal("123456789", request.Arguments["objectIdentifier"]);
        Assert.Equal("===+++УСПД_Э3#-A1", request.Arguments["stringIdentifier"]);
        Assert.Equal(4, request.Arguments.Count);

        Assert.Equal(target.ProjectSessionId, plan.ProjectSessionId);
        Assert.Equal(new DateTime(2026, 10, 27, 12, 30, 0, DateTimeKind.Utc), plan.CreatedUtc);
        Assert.Equal(ChangePlanStatuses.Previewed, plan.Status);
        ChangeOperation operation = Assert.Single(plan.Operations);
        Assert.Equal("setFunctionText", operation.Kind);
        Assert.Equal("Old displayed text", operation.OldValue);
        Assert.Equal("New text", operation.NewValue);
        Assert.Equal(target.StringIdentifier, operation.Target.StringIdentifier);
        Assert.Same(plan, store.Get(plan.ChangePlanId));
    }

    [Fact]
    public async Task Preview_AcceptsExactly1024Utf16CodeUnits()
    {
        var bridge = new RecordingBridgeClient("{\"kind\":\"Function\",\"functionText\":\"Old\"}");
        var planner = CreatePlanner(bridge);
        string text = new string('x', 1024);

        ChangePlan plan = await planner.PreviewAsync(CreateTarget(), text, CancellationToken.None);

        Assert.Equal(text, Assert.Single(plan.Operations).NewValue);
        Assert.Single(bridge.Requests);
    }

    [Fact]
    public async Task Preview_Rejects1025Utf16CodeUnitsBeforeBridgeCall()
    {
        var bridge = new RecordingBridgeClient();
        var planner = CreatePlanner(bridge);

        ChangePlanException error = await Assert.ThrowsAsync<ChangePlanException>(
            () => planner.PreviewAsync(CreateTarget(), new string('x', 1025), CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.ValidationFailed, error.ErrorCode);
        Assert.Empty(bridge.Requests);
    }

    [Fact]
    public async Task Preview_RejectsNullTextBeforeBridgeCall()
    {
        var bridge = new RecordingBridgeClient();
        var planner = CreatePlanner(bridge);

        ChangePlanException error = await Assert.ThrowsAsync<ChangePlanException>(
            () => planner.PreviewAsync(CreateTarget(), null, CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.ValidationFailed, error.ErrorCode);
        Assert.Empty(bridge.Requests);
    }

    [Fact]
    public async Task Preview_PreservesKnownStableBridgeFailure()
    {
        var bridge = new RecordingBridgeClient(responseFactory: request => new BridgeResponse
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = request.RequestId,
            Success = false,
            ErrorCode = BridgeErrorCodes.SessionStale,
            ErrorMessage = "Project session is stale."
        });
        var planner = CreatePlanner(bridge);

        ChangePlanException error = await Assert.ThrowsAsync<ChangePlanException>(
            () => planner.PreviewAsync(CreateTarget(), "New", CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.SessionStale, error.ErrorCode);
    }

    [Fact]
    public async Task Preview_MapsUnknownBridgeFailureToInternalError()
    {
        var bridge = new RecordingBridgeClient(responseFactory: request => new BridgeResponse
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = request.RequestId,
            Success = false,
            ErrorCode = "SOMETHING_UNEXPECTED",
            ErrorMessage = "Unexpected failure."
        });
        var planner = CreatePlanner(bridge);

        ChangePlanException error = await Assert.ThrowsAsync<ChangePlanException>(
            () => planner.PreviewAsync(CreateTarget(), "New", CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.InternalError, error.ErrorCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"kind\":\"Page\",\"functionText\":\"Old\"}")]
    [InlineData("{\"kind\":\"Function\",\"functionText\":42}")]
    public async Task Preview_RejectsMalformedOrNonFunctionPayload(string dataJson)
    {
        var bridge = new RecordingBridgeClient(dataJson);
        var planner = CreatePlanner(bridge);

        ChangePlanException error = await Assert.ThrowsAsync<ChangePlanException>(
            () => planner.PreviewAsync(CreateTarget(), "New", CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.InternalError, error.ErrorCode);
    }

    [Fact]
    public async Task Preview_NormalizesNullFunctionTextToEmptyString()
    {
        var bridge = new RecordingBridgeClient("{\"kind\":\"Function\",\"functionText\":null}");
        var planner = CreatePlanner(bridge);

        ChangePlan plan = await planner.PreviewAsync(CreateTarget(), "New", CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(plan.Operations).OldValue);
    }

    [Fact]
    public async Task Preview_PlanTargetCannotBeChangedThroughCallerOrGetterMutation()
    {
        var bridge = new RecordingBridgeClient("{\"kind\":\"Function\",\"functionText\":\"Old\"}");
        var store = new ChangePlanStore();
        var planner = new FunctionTextChangePlanner(bridge, store, TimeProvider.System);
        ObjectReference target = CreateTarget();
        Guid originalSession = target.ProjectSessionId;

        ChangePlan plan = await planner.PreviewAsync(target, "New", CancellationToken.None);
        target.StringIdentifier = "mutated-source";
        target.ProjectSessionId = Guid.NewGuid();
        ObjectReference exposed = Assert.Single(plan.Operations).Target;
        exposed.StringIdentifier = "mutated-getter";

        ObjectReference stored = Assert.Single(store.Get(plan.ChangePlanId).Operations).Target;
        Assert.Equal(originalSession, stored.ProjectSessionId);
        Assert.Equal("===+++УСПД_Э3#-A1", stored.StringIdentifier);
    }

    [Fact]
    public async Task Preview_SnapshotsTargetBeforeAwaitedReadCompletes()
    {
        var bridge = new DeferredBridgeClient();
        var store = new ChangePlanStore();
        var planner = new FunctionTextChangePlanner(bridge, store, TimeProvider.System);
        ObjectReference target = CreateTarget();
        Guid originalSession = target.ProjectSessionId;

        Task<ChangePlan> preview = planner.PreviewAsync(target, "New", CancellationToken.None);
        BridgeRequest request = await bridge.RequestReceived.WaitAsync(TimeSpan.FromSeconds(5));

        target.ProjectSessionId = Guid.NewGuid();
        target.DatabaseIdentifier = 999;
        target.TypeIdentifier = 888;
        target.ObjectIdentifier = 777;
        target.StringIdentifier = "mutated-during-read";

        bridge.Complete(request, "{\"kind\":\"Function\",\"functionText\":\"Old\"}");
        ChangePlan plan = await preview;

        Assert.Equal(originalSession, request.ProjectSessionId);
        Assert.Equal("25", request.Arguments["databaseIdentifier"]);
        Assert.Equal("177", request.Arguments["typeIdentifier"]);
        Assert.Equal("123456789", request.Arguments["objectIdentifier"]);
        Assert.Equal("===+++УСПД_Э3#-A1", request.Arguments["stringIdentifier"]);

        ObjectReference stored = Assert.Single(plan.Operations).Target;
        Assert.Equal(originalSession, plan.ProjectSessionId);
        Assert.Equal(originalSession, stored.ProjectSessionId);
        Assert.Equal(25u, stored.DatabaseIdentifier);
        Assert.Equal(177u, stored.TypeIdentifier);
        Assert.Equal(123456789L, stored.ObjectIdentifier);
        Assert.Equal("===+++УСПД_Э3#-A1", stored.StringIdentifier);
    }

    private static FunctionTextChangePlanner CreatePlanner(RecordingBridgeClient bridge)
    {
        return new FunctionTextChangePlanner(bridge, new ChangePlanStore(), TimeProvider.System);
    }

    private static ObjectReference CreateTarget()
    {
        return new ObjectReference
        {
            ProjectSessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DatabaseIdentifier = 25,
            TypeIdentifier = 177,
            ObjectIdentifier = 123456789,
            StringIdentifier = "===+++УСПД_Э3#-A1"
        };
    }

    private sealed class RecordingBridgeClient : IBridgeClient
    {
        private readonly string _dataJson;
        private readonly Func<BridgeRequest, BridgeResponse> _responseFactory;

        public RecordingBridgeClient(
            string dataJson = "{\"kind\":\"Function\",\"functionText\":\"Old\"}",
            Func<BridgeRequest, BridgeResponse> responseFactory = null)
        {
            _dataJson = dataJson;
            _responseFactory = responseFactory;
        }

        public List<BridgeRequest> Requests { get; } = new();

        public Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responseFactory?.Invoke(request) ?? new BridgeResponse
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = request.RequestId,
                Success = true,
                DataJson = _dataJson
            });
        }
    }

    private sealed class DeferredBridgeClient : IBridgeClient
    {
        private readonly TaskCompletionSource<BridgeRequest> _requestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<BridgeResponse> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<BridgeRequest> RequestReceived => _requestReceived.Task;

        public Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            _requestReceived.TrySetResult(request);
            return _response.Task;
        }

        public void Complete(BridgeRequest request, string dataJson)
        {
            _response.TrySetResult(new BridgeResponse
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = request.RequestId,
                Success = true,
                DataJson = dataJson
            });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
