using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Transport;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class ChangePlanApplyServiceTests
{
    [Fact]
    public async Task ApplyAsync_EmptyPlanId_FailsBeforeBridge()
    {
        var bridge = new RecordingBridge();
        var service = new ChangePlanApplyService(new ChangePlanStore(), bridge);

        var error = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(Guid.Empty, CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.ValidationFailed, error.ErrorCode);
        Assert.Empty(bridge.Requests);
    }

    [Fact]
    public async Task ApplyAsync_MissingPlan_FailsBeforeBridge()
    {
        var bridge = new RecordingBridge();
        var service = new ChangePlanApplyService(new ChangePlanStore(), bridge);

        var error = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.PlanNotFound, error.ErrorCode);
        Assert.Empty(bridge.Requests);
    }

    [Fact]
    public async Task ApplyAsync_UsesOnlyStoredImmutablePayloadAndMarksApplied()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan();
        store.Add(plan);
        var bridge = new RecordingBridge
        {
            NextResponse = Success("{\"status\":\"applied\"}")
        };
        var service = new ChangePlanApplyService(store, bridge);

        ChangePlan result = await service.ApplyAsync(plan.ChangePlanId, CancellationToken.None);

        Assert.Same(plan, result);
        Assert.Equal(ChangePlanStatuses.Applied, plan.Status);
        BridgeRequest request = Assert.Single(bridge.Requests);
        Assert.Equal("change.apply", request.Operation);
        Assert.Equal("write", request.Mode);
        Assert.Equal(plan.ProjectSessionId, request.ProjectSessionId);
        Assert.Equal(7, request.Arguments.Count);
        Assert.Equal(plan.ChangePlanId.ToString("D"), request.Arguments["changePlanId"]);
        Assert.Equal("25", request.Arguments["databaseIdentifier"]);
        Assert.Equal("177", request.Arguments["typeIdentifier"]);
        Assert.Equal("123456789", request.Arguments["objectIdentifier"]);
        Assert.Equal("===+++TEST#-A1", request.Arguments["stringIdentifier"]);
        Assert.Equal("Old text", request.Arguments["oldValue"]);
        Assert.Equal("New text", request.Arguments["newValue"]);
    }

    [Fact]
    public async Task ApplyAsync_BridgeFailure_MarksPlanFailedAndPreservesStableCode()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan();
        store.Add(plan);
        var bridge = new RecordingBridge
        {
            NextResponse = Failure(BridgeErrorCodes.PlanStale, "Preview became stale.")
        };
        var service = new ChangePlanApplyService(store, bridge);

        var error = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(plan.ChangePlanId, CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.PlanStale, error.ErrorCode);
        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);
        Assert.Single(bridge.Requests);
    }

    [Fact]
    public async Task ApplyAsync_UnknownBridgeError_MarksPlanFailedAndFailsClosed()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan();
        store.Add(plan);
        var bridge = new RecordingBridge
        {
            NextResponse = Failure("SOMETHING_NEW", "Unexpected.")
        };
        var service = new ChangePlanApplyService(store, bridge);

        var error = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(plan.ChangePlanId, CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.InternalError, error.ErrorCode);
        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);
    }

    [Fact]
    public async Task ApplyAsync_TransportException_MarksPlanFailedAndCannotRetry()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan();
        store.Add(plan);
        var bridge = new RecordingBridge { ThrowOnSend = true };
        var service = new ChangePlanApplyService(store, bridge);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyAsync(plan.ChangePlanId, CancellationToken.None));
        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);

        var retry = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(plan.ChangePlanId, CancellationToken.None));
        Assert.Equal(BridgeErrorCodes.PlanAlreadyApplied, retry.ErrorCode);
        Assert.Single(bridge.Requests);
    }

    [Fact]
    public async Task ApplyAsync_NonSingleSetFunctionTextPlan_FailsClosedAfterClaim()
    {
        var store = new ChangePlanStore();
        ChangePlan plan = CreatePlan("otherOperation");
        store.Add(plan);
        var bridge = new RecordingBridge();
        var service = new ChangePlanApplyService(store, bridge);

        var error = await Assert.ThrowsAsync<ChangePlanException>(
            () => service.ApplyAsync(plan.ChangePlanId, CancellationToken.None));

        Assert.Equal(BridgeErrorCodes.ValidationFailed, error.ErrorCode);
        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);
        Assert.Empty(bridge.Requests);
    }

    private static ChangePlan CreatePlan(string kind = "setFunctionText")
    {
        Guid session = Guid.NewGuid();
        var target = new ObjectReference
        {
            ProjectSessionId = session,
            DatabaseIdentifier = 25,
            TypeIdentifier = 177,
            ObjectIdentifier = 123456789,
            StringIdentifier = "===+++TEST#-A1"
        };

        return new ChangePlan(
            Guid.NewGuid(),
            new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc),
            session,
            new[] { new ChangeOperation(kind, target, "Old text", "New text") });
    }

    private static BridgeResponse Success(string dataJson) => new()
    {
        RequestId = Guid.NewGuid(),
        Success = true,
        DataJson = dataJson
    };

    private static BridgeResponse Failure(string code, string message) => new()
    {
        RequestId = Guid.NewGuid(),
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };

    private sealed class RecordingBridge : IBridgeClient
    {
        public List<BridgeRequest> Requests { get; } = new();
        public BridgeResponse NextResponse { get; set; } = Success("{\"status\":\"applied\"}");
        public bool ThrowOnSend { get; set; }

        public Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("Injected transport failure.");
            }

            NextResponse.RequestId = request.RequestId;
            return Task.FromResult(NextResponse);
        }
    }
}
