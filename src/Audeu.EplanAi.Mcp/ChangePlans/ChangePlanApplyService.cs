using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Transport;

namespace Audeu.EplanAi.Mcp.ChangePlans;

public sealed class ChangePlanApplyService
{
    private static readonly HashSet<string> StableBridgeErrorCodes = new(StringComparer.Ordinal)
    {
        BridgeErrorCodes.ProtocolMismatch,
        BridgeErrorCodes.EplanNotFound,
        BridgeErrorCodes.EplanNotConnected,
        BridgeErrorCodes.EplanActionFailed,
        BridgeErrorCodes.OperationTimeout,
        BridgeErrorCodes.NoActiveProject,
        BridgeErrorCodes.SessionStale,
        BridgeErrorCodes.ObjectNotFound,
        BridgeErrorCodes.ObjectInvalid,
        BridgeErrorCodes.ObjectTypeNotAllowed,
        BridgeErrorCodes.ReadOnlyProject,
        BridgeErrorCodes.ValidationFailed,
        BridgeErrorCodes.PlanNotFound,
        BridgeErrorCodes.PlanAlreadyApplied,
        BridgeErrorCodes.PlanStale,
        BridgeErrorCodes.BridgeUnknownOperation,
        BridgeErrorCodes.InternalError
    };

    private readonly ChangePlanStore _store;
    private readonly IBridgeClient _bridge;

    public ChangePlanApplyService(ChangePlanStore store, IBridgeClient bridge)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public async Task<ChangePlan> ApplyAsync(Guid changePlanId, CancellationToken cancellationToken)
    {
        if (changePlanId == Guid.Empty)
        {
            throw Error(BridgeErrorCodes.ValidationFailed, "changePlanId must be a non-empty GUID.");
        }

        ChangePlan snapshot = _store.Get(changePlanId);
        ChangePlan plan = _store.ClaimForApply(changePlanId, snapshot.ProjectSessionId);

        try
        {
            ChangeOperation operation = ValidateAndGetOperation(plan);
            ObjectReference target = operation.Target;
            var request = new BridgeRequest
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = Guid.NewGuid(),
                Operation = "change.apply",
                Mode = "write",
                ProjectSessionId = plan.ProjectSessionId,
                Arguments = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changePlanId"] = plan.ChangePlanId.ToString("D"),
                    ["databaseIdentifier"] = target.DatabaseIdentifier.ToString(CultureInfo.InvariantCulture),
                    ["typeIdentifier"] = target.TypeIdentifier.ToString(CultureInfo.InvariantCulture),
                    ["objectIdentifier"] = target.ObjectIdentifier.ToString(CultureInfo.InvariantCulture),
                    ["stringIdentifier"] = target.StringIdentifier,
                    ["oldValue"] = operation.OldValue,
                    ["newValue"] = operation.NewValue
                }
            };

            BridgeResponse response = await _bridge
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response is null)
            {
                throw Error(BridgeErrorCodes.InternalError, "The EPLAN bridge returned no apply response.");
            }

            if (!response.Success)
            {
                string code = StableBridgeErrorCodes.Contains(response.ErrorCode ?? string.Empty)
                    ? response.ErrorCode!
                    : BridgeErrorCodes.InternalError;
                string message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? "The EPLAN bridge rejected the change plan."
                    : response.ErrorMessage!;
                throw Error(code, message);
            }

            _store.MarkApplied(plan.ChangePlanId);
            return plan;
        }
        catch
        {
            if (string.Equals(plan.Status, ChangePlanStatuses.Previewed, StringComparison.Ordinal))
            {
                _store.MarkFailed(plan.ChangePlanId);
            }

            throw;
        }
    }

    private static ChangeOperation ValidateAndGetOperation(ChangePlan plan)
    {
        if (plan.Operations.Count != 1)
        {
            throw Error(
                BridgeErrorCodes.ValidationFailed,
                "A v0.1 change plan must contain exactly one operation.");
        }

        ChangeOperation operation = plan.Operations[0];
        if (!string.Equals(operation.Kind, "setFunctionText", StringComparison.Ordinal))
        {
            throw Error(
                BridgeErrorCodes.ValidationFailed,
                "Only setFunctionText change plans are supported in v0.1.");
        }

        ObjectReference target = operation.Target;
        if (target.ProjectSessionId != plan.ProjectSessionId
            || target.ProjectSessionId == Guid.Empty
            || string.IsNullOrWhiteSpace(target.StringIdentifier))
        {
            throw Error(
                BridgeErrorCodes.ValidationFailed,
                "The stored change plan target is invalid.");
        }

        return operation;
    }

    private static ChangePlanException Error(string code, string message) => new(code, message);
}
