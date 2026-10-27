using System;
using System.Collections.Generic;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.Mcp.ChangePlans;

public sealed class ChangePlanStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ChangePlan> _plans = new();
    private readonly HashSet<Guid> _claimedForApply = new();

    public void Add(ChangePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        lock (_sync)
        {
            if (_plans.ContainsKey(plan.ChangePlanId))
            {
                throw Error(
                    BridgeErrorCodes.ValidationFailed,
                    $"Change plan '{plan.ChangePlanId}' already exists.");
            }

            if (!string.Equals(plan.Status, ChangePlanStatuses.Previewed, StringComparison.Ordinal))
            {
                throw Error(
                    BridgeErrorCodes.ValidationFailed,
                    "Only previewed change plans can be stored.");
            }

            _plans.Add(plan.ChangePlanId, plan);
        }
    }

    public ChangePlan Get(Guid planId)
    {
        lock (_sync)
        {
            return GetLocked(planId);
        }
    }

    public ChangePlan GetForSession(Guid planId, Guid currentProjectSessionId)
    {
        lock (_sync)
        {
            return GetForSessionLocked(planId, currentProjectSessionId);
        }
    }

    public ChangePlan ClaimForApply(Guid planId, Guid currentProjectSessionId)
    {
        lock (_sync)
        {
            var plan = GetForSessionLocked(planId, currentProjectSessionId);
            if (!string.Equals(plan.Status, ChangePlanStatuses.Previewed, StringComparison.Ordinal)
                || !_claimedForApply.Add(planId))
            {
                throw Error(
                    BridgeErrorCodes.PlanAlreadyApplied,
                    $"Change plan '{planId}' cannot be applied more than once.");
            }

            return plan;
        }
    }

    public void MarkApplied(Guid planId)
    {
        lock (_sync)
        {
            var plan = GetClaimedPreviewedLocked(planId);
            plan.MarkApplied();
        }
    }

    public void MarkFailed(Guid planId)
    {
        lock (_sync)
        {
            var plan = GetClaimedPreviewedLocked(planId);
            plan.MarkFailed();
        }
    }

    private ChangePlan GetLocked(Guid planId)
    {
        if (!_plans.TryGetValue(planId, out var plan))
        {
            throw Error(
                BridgeErrorCodes.PlanNotFound,
                $"Change plan '{planId}' was not found.");
        }

        return plan;
    }

    private ChangePlan GetForSessionLocked(Guid planId, Guid currentProjectSessionId)
    {
        var plan = GetLocked(planId);
        if (plan.ProjectSessionId != currentProjectSessionId)
        {
            throw Error(
                BridgeErrorCodes.PlanStale,
                $"Change plan '{planId}' does not belong to the current project session.");
        }

        return plan;
    }

    private ChangePlan GetClaimedPreviewedLocked(Guid planId)
    {
        var plan = GetLocked(planId);
        if (!_claimedForApply.Contains(planId)
            || !string.Equals(plan.Status, ChangePlanStatuses.Previewed, StringComparison.Ordinal))
        {
            throw Error(
                BridgeErrorCodes.ValidationFailed,
                $"Change plan '{planId}' is not an active claimed preview.");
        }

        return plan;
    }

    private static ChangePlanException Error(string code, string message)
    {
        return new ChangePlanException(code, message);
    }
}
