using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.ChangePlans;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class ChangePlanStoreTests
{
    [Fact]
    public void AddThenGet_ReturnsStoredPlan()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();

        store.Add(plan);

        Assert.Same(plan, store.Get(plan.ChangePlanId));
    }

    [Fact]
    public void Get_MissingPlan_ThrowsPlanNotFound()
    {
        var store = new ChangePlanStore();

        var error = Assert.Throws<ChangePlanException>(() => store.Get(Guid.NewGuid()));

        Assert.Equal(BridgeErrorCodes.PlanNotFound, error.ErrorCode);
    }

    [Fact]
    public void GetForSession_MismatchedSession_ThrowsPlanStale()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();
        store.Add(plan);

        var error = Assert.Throws<ChangePlanException>(
            () => store.GetForSession(plan.ChangePlanId, Guid.NewGuid()));

        Assert.Equal(BridgeErrorCodes.PlanStale, error.ErrorCode);
    }

    [Fact]
    public void ClaimForApply_FirstSucceeds_SecondIsRejected()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();
        store.Add(plan);

        Assert.Same(plan, store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId));
        var error = Assert.Throws<ChangePlanException>(
            () => store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId));

        Assert.Equal(BridgeErrorCodes.PlanAlreadyApplied, error.ErrorCode);
    }

    [Fact]
    public void MarkApplied_ClaimedPlanBecomesAppliedAndCannotBeClaimedAgain()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();
        store.Add(plan);
        store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId);

        store.MarkApplied(plan.ChangePlanId);

        Assert.Equal(ChangePlanStatuses.Applied, plan.Status);
        var error = Assert.Throws<ChangePlanException>(
            () => store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId));
        Assert.Equal(BridgeErrorCodes.PlanAlreadyApplied, error.ErrorCode);
    }

    [Fact]
    public void MarkFailed_ClaimedPlanBecomesTerminalAndCannotBeClaimedAgain()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();
        store.Add(plan);
        store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId);

        store.MarkFailed(plan.ChangePlanId);

        Assert.Equal(ChangePlanStatuses.Failed, plan.Status);
        var error = Assert.Throws<ChangePlanException>(
            () => store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId));
        Assert.Equal(BridgeErrorCodes.PlanAlreadyApplied, error.ErrorCode);
    }

    [Fact]
    public async Task ClaimForApply_ConcurrentCallers_ExactlyOneSucceeds()
    {
        var store = new ChangePlanStore();
        var plan = CreatePlan();
        store.Add(plan);

        Task<string>[] claims = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => Claim(store, plan)))
            .ToArray();

        string[] results = await Task.WhenAll(claims);

        Assert.Equal(1, results.Count(result => result == "success"));
        Assert.Equal(15, results.Count(result => result == BridgeErrorCodes.PlanAlreadyApplied));
    }

    private static string Claim(ChangePlanStore store, ChangePlan plan)
    {
        try
        {
            store.ClaimForApply(plan.ChangePlanId, plan.ProjectSessionId);
            return "success";
        }
        catch (ChangePlanException error)
        {
            return error.ErrorCode;
        }
    }

    private static ChangePlan CreatePlan(Guid? sessionId = null)
    {
        var session = sessionId ?? Guid.NewGuid();
        var target = new ObjectReference
        {
            ProjectSessionId = session,
            DatabaseIdentifier = 7,
            TypeIdentifier = 27,
            ObjectIdentifier = 1482,
            StringIdentifier = "Function:1482"
        };

        return new ChangePlan(
            Guid.NewGuid(),
            new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc),
            session,
            new[] { new ChangeOperation("setFunctionText", target, "Old", "New") });
    }
}
