using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.Contracts.Tests;

public sealed class ChangePlanTests
{
    [Fact]
    public void ObjectReference_JsonRoundTripPreservesSessionScopedEplanIdentity()
    {
        var sessionId = Guid.NewGuid();
        var reference = new ObjectReference
        {
            ProjectSessionId = sessionId,
            DatabaseIdentifier = 7u,
            TypeIdentifier = 27u,
            ObjectIdentifier = 1482L,
            StringIdentifier = "function:1482"
        };

        var json = JsonSerializer.Serialize(reference);
        var roundTripped = JsonSerializer.Deserialize<ObjectReference>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(sessionId, roundTripped.ProjectSessionId);
        Assert.Equal(7u, roundTripped.DatabaseIdentifier);
        Assert.Equal(27u, roundTripped.TypeIdentifier);
        Assert.Equal(1482L, roundTripped.ObjectIdentifier);
        Assert.Equal("function:1482", roundTripped.StringIdentifier);
    }

    [Fact]
    public void BridgeErrorCodes_AreStableProtocolValues()
    {
        Assert.Equal("PROTOCOL_MISMATCH", BridgeErrorCodes.ProtocolMismatch);
        Assert.Equal("SESSION_STALE", BridgeErrorCodes.SessionStale);
        Assert.Equal("PLAN_NOT_FOUND", BridgeErrorCodes.PlanNotFound);
        Assert.Equal("PLAN_ALREADY_APPLIED", BridgeErrorCodes.PlanAlreadyApplied);
        Assert.Equal("PLAN_STALE", BridgeErrorCodes.PlanStale);
        Assert.Equal("BRIDGE_UNKNOWN_OPERATION", BridgeErrorCodes.BridgeUnknownOperation);
    }

    [Fact]
    public void ChangeOperation_SnapshotsTargetAtConstruction()
    {
        var target = CreateReference();
        var expectedSession = target.ProjectSessionId;
        var operation = new ChangeOperation("setFunctionText", target, "Old", "New");

        target.ProjectSessionId = Guid.NewGuid();
        target.DatabaseIdentifier = 99u;
        target.TypeIdentifier = 88u;
        target.ObjectIdentifier = 7777L;
        target.StringIdentifier = "mutated";

        var stored = operation.Target;
        Assert.Equal(expectedSession, stored.ProjectSessionId);
        Assert.Equal(7u, stored.DatabaseIdentifier);
        Assert.Equal(27u, stored.TypeIdentifier);
        Assert.Equal(1482L, stored.ObjectIdentifier);
        Assert.Equal("function:1482", stored.StringIdentifier);
    }

    [Fact]
    public void ChangeOperation_TargetGetterReturnsDefensiveCopy()
    {
        var operation = new ChangeOperation("setFunctionText", CreateReference(), "Old", "New");

        var first = operation.Target;
        first.DatabaseIdentifier = 999u;
        first.StringIdentifier = "mutated";

        var second = operation.Target;
        Assert.NotSame(first, second);
        Assert.Equal(7u, second.DatabaseIdentifier);
        Assert.Equal("function:1482", second.StringIdentifier);
    }

    [Fact]
    public void ChangePlan_CopiesOperationsAndStartsPreviewed()
    {
        var target = CreateReference();
        var operation = new ChangeOperation("setFunctionText", target, "Old", "New");
        var source = new List<ChangeOperation> { operation };
        var planId = Guid.NewGuid();
        var sessionId = target.ProjectSessionId;
        var createdUtc = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

        var plan = new ChangePlan(planId, createdUtc, sessionId, source);
        source.Add(new ChangeOperation("setFunctionText", target, "Other", "Value"));

        Assert.Equal(planId, plan.ChangePlanId);
        Assert.Equal(createdUtc, plan.CreatedUtc);
        Assert.Equal(sessionId, plan.ProjectSessionId);
        Assert.Equal(ChangePlanStatuses.Previewed, plan.Status);
        Assert.Single(plan.Operations);
        Assert.Same(operation, plan.Operations.Single());
    }

    [Fact]
    public void ChangePlan_IdentityAndOperationsHaveNoPublicSetters()
    {
        Assert.Null(typeof(ChangePlan).GetProperty(nameof(ChangePlan.ChangePlanId))!.SetMethod);
        Assert.Null(typeof(ChangePlan).GetProperty(nameof(ChangePlan.CreatedUtc))!.SetMethod);
        Assert.Null(typeof(ChangePlan).GetProperty(nameof(ChangePlan.ProjectSessionId))!.SetMethod);
        Assert.Null(typeof(ChangePlan).GetProperty(nameof(ChangePlan.Operations))!.SetMethod);
    }

    [Fact]
    public void ChangePlan_CanTransitionFromPreviewedToApplied()
    {
        var plan = CreatePlan();

        plan.MarkApplied();

        Assert.Equal(ChangePlanStatuses.Applied, plan.Status);
    }

    [Fact]
    public void ChangePlan_CanTransitionFromPreviewedToFailed()
    {
        var plan = CreatePlan();

        plan.MarkFailed();

        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);
    }

    [Fact]
    public void ChangePlan_DoubleApplyIsForbidden()
    {
        var plan = CreatePlan();
        plan.MarkApplied();

        Assert.Throws<InvalidOperationException>(() => plan.MarkApplied());
    }

    [Fact]
    public void ChangePlan_FailedPlanCannotLaterApply()
    {
        var plan = CreatePlan();
        plan.MarkFailed();

        Assert.Throws<InvalidOperationException>(() => plan.MarkApplied());
    }

    [Fact]
    public void ChangePlan_AppliedPlanCannotLaterFail()
    {
        var plan = CreatePlan();
        plan.MarkApplied();

        Assert.Throws<InvalidOperationException>(() => plan.MarkFailed());
    }

    private static ChangePlan CreatePlan()
    {
        var reference = CreateReference();
        var operation = new ChangeOperation("setFunctionText", reference, "Old", "New");
        return new ChangePlan(
            Guid.NewGuid(),
            new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc),
            reference.ProjectSessionId,
            new[] { operation });
    }

    private static ObjectReference CreateReference()
    {
        return new ObjectReference
        {
            ProjectSessionId = Guid.NewGuid(),
            DatabaseIdentifier = 7u,
            TypeIdentifier = 27u,
            ObjectIdentifier = 1482L,
            StringIdentifier = "function:1482"
        };
    }
}
