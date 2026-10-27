using System;
using System.Linq;
using Xunit;

namespace Audeu.EplanAi.McpProbe.Tests;

public sealed class GateCProbeContractTests
{
    [Fact]
    public void ExpectedTools_ContainsExactlyEightApprovedTask12Tools()
    {
        Assert.Equal(
            new[]
            {
                "eplan_apply_change_plan",
                "eplan_get_active_project",
                "eplan_get_object",
                "eplan_get_selection",
                "eplan_list_pages",
                "eplan_preview_set_function_text",
                "eplan_status",
                "eplan_undo_last_ai_change"
            },
            GateCProbeContract.ExpectedTools.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ParsePreviewPlan_RequiresSingleSetFunctionTextAndReturnsImmutableValues()
    {
        Guid planId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        string json = "{"
            + "\"changePlanId\":\"" + planId.ToString("D") + "\","
            + "\"projectSessionId\":\"" + sessionId.ToString("D") + "\","
            + "\"status\":\"previewed\","
            + "\"operations\":[{"
            + "\"kind\":\"setFunctionText\","
            + "\"target\":{\"projectSessionId\":\"" + sessionId.ToString("D") + "\",\"databaseIdentifier\":25,\"typeIdentifier\":177,\"objectIdentifier\":123456789,\"stringIdentifier\":\"===+++TEST#-A1\"},"
            + "\"oldValue\":\"Old text\",\"newValue\":\"New text\""
            + "}]}";

        GateCChangePlan plan = GateCProbeContract.ParsePreviewPlan(json);

        Assert.Equal(planId, plan.ChangePlanId);
        Assert.Equal(sessionId, plan.ProjectSessionId);
        Assert.Equal("previewed", plan.Status);
        Assert.Equal("Old text", plan.OldValue);
        Assert.Equal("New text", plan.NewValue);
        Assert.Equal((uint)25, plan.DatabaseIdentifier);
        Assert.Equal((uint)177, plan.TypeIdentifier);
        Assert.Equal(123456789L, plan.ObjectIdentifier);
        Assert.Equal("===+++TEST#-A1", plan.StringIdentifier);
    }

    [Fact]
    public void ParsePreviewPlan_WrongOperationKind_FailsClosed()
    {
        Guid planId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        string json = "{\"changePlanId\":\"" + planId.ToString("D")
            + "\",\"projectSessionId\":\"" + sessionId.ToString("D")
            + "\",\"status\":\"previewed\",\"operations\":[{\"kind\":\"other\",\"target\":{\"projectSessionId\":\""
            + sessionId.ToString("D")
            + "\",\"databaseIdentifier\":25,\"typeIdentifier\":177,\"objectIdentifier\":1,\"stringIdentifier\":\"X\"},\"oldValue\":\"A\",\"newValue\":\"B\"}]}";

        Assert.Throws<InvalidOperationException>(() => GateCProbeContract.ParsePreviewPlan(json));
    }

    [Fact]
    public void RequireAppliedPlan_RequiresSamePlanIdAndAppliedStatus()
    {
        Guid planId = Guid.NewGuid();
        string json = "{\"changePlanId\":\"" + planId.ToString("D") + "\",\"status\":\"applied\"}";

        GateCProbeContract.RequireAppliedPlan(json, planId);

        Assert.Throws<InvalidOperationException>(
            () => GateCProbeContract.RequireAppliedPlan(json, Guid.NewGuid()));
    }

    [Fact]
    public void RequireUndoResult_RequiresUndoneStatus()
    {
        GateCProbeContract.RequireUndoResult("{\"status\":\"undone\"}");

        Assert.Throws<InvalidOperationException>(
            () => GateCProbeContract.RequireUndoResult("{\"status\":\"somethingElse\"}"));
    }

    [Fact]
    public void RequireDisposableProjectConfirmation_RequiresThreeExactNonEmptyMatches()
    {
        GateCProbeContract.RequireDisposableProjectConfirmation("Scratch", "Scratch", "Scratch");

        Assert.Throws<InvalidOperationException>(
            () => GateCProbeContract.RequireDisposableProjectConfirmation("Scratch", "Scratch", "Production"));
        Assert.Throws<InvalidOperationException>(
            () => GateCProbeContract.RequireDisposableProjectConfirmation("Scratch", "Other", "Other"));
        Assert.Throws<InvalidOperationException>(
            () => GateCProbeContract.RequireDisposableProjectConfirmation("", "", ""));
    }
}
