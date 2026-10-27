using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Transport;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Audeu.EplanAi.Mcp.Tools;

public sealed class EplanWriteTools
{
    private static readonly HashSet<string> StableErrorCodes = new(StringComparer.Ordinal)
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

    private static readonly JsonSerializerOptions PlanJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(
        Name = "eplan_preview_set_function_text",
        ReadOnly = true,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Creates an immutable preview plan for changing Function Text. This reads EPLAN but does not modify it.")]
    public static async Task<string> PreviewSetFunctionTextAsync(
        [Description("Project session identifier returned by eplan_get_active_project.")]
        Guid projectSessionId,
        [Description("Compact object database identifier returned by eplan_get_selection.")]
        uint databaseIdentifier,
        [Description("Compact object type identifier returned by eplan_get_selection.")]
        uint typeIdentifier,
        [Description("Compact numeric object identifier returned by eplan_get_selection.")]
        long objectIdentifier,
        [Description("Compact string object identifier returned by eplan_get_selection.")]
        string stringIdentifier,
        [Description("Proposed displayed Function Text, up to 1024 UTF-16 code units.")]
        string newText,
        FunctionTextChangePlanner planner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planner);

        var target = new ObjectReference
        {
            ProjectSessionId = projectSessionId,
            DatabaseIdentifier = databaseIdentifier,
            TypeIdentifier = typeIdentifier,
            ObjectIdentifier = objectIdentifier,
            StringIdentifier = stringIdentifier
        };

        try
        {
            ChangePlan plan = await planner
                .PreviewAsync(target, newText, cancellationToken)
                .ConfigureAwait(false);
            return SerializePlan(plan);
        }
        catch (ChangePlanException error)
        {
            throw ControlledError(NormalizeErrorCode(error.ErrorCode), error.Message);
        }
    }

    [McpServerTool(
        Name = "eplan_apply_change_plan",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Applies one previously previewed immutable EPLAN change plan by its identifier. The previewed target and values cannot be changed during apply.")]
    public static async Task<string> ApplyChangePlanAsync(
        [Description("Change plan identifier returned by eplan_preview_set_function_text.")]
        Guid changePlanId,
        ChangePlanApplyService applyService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applyService);

        try
        {
            ChangePlan plan = await applyService
                .ApplyAsync(changePlanId, cancellationToken)
                .ConfigureAwait(false);
            return SerializePlan(plan);
        }
        catch (ChangePlanException error)
        {
            throw ControlledError(NormalizeErrorCode(error.ErrorCode), error.Message);
        }
    }

    [McpServerTool(
        Name = "eplan_undo_last_ai_change",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Reverses the last successfully applied AI Function Text change for the current EPLAN project session.")]
    public static async Task<string> UndoLastAiChangeAsync(
        [Description("Current project session identifier returned by eplan_get_active_project.")]
        Guid projectSessionId,
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken)
    {
        if (projectSessionId == Guid.Empty)
        {
            throw ControlledError(
                BridgeErrorCodes.ValidationFailed,
                "projectSessionId must be a non-empty GUID.");
        }

        ArgumentNullException.ThrowIfNull(bridgeClient);

        var request = new BridgeRequest
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = Guid.NewGuid(),
            Operation = "change.undoLast",
            Mode = "write",
            ProjectSessionId = projectSessionId,
            Arguments = new Dictionary<string, string>()
        };

        BridgeResponse response = await bridgeClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            throw ControlledError(
                BridgeErrorCodes.InternalError,
                "The EPLAN bridge returned no undo response.");
        }

        if (!response.Success)
        {
            string errorCode = NormalizeErrorCode(response.ErrorCode);
            string message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                ? "EPLAN bridge undo operation failed."
                : response.ErrorMessage!;
            throw ControlledError(errorCode, message);
        }

        if (string.IsNullOrWhiteSpace(response.DataJson))
        {
            throw ControlledError(
                BridgeErrorCodes.InternalError,
                "EPLAN bridge returned no JSON result for undo.");
        }

        return response.DataJson;
    }

    private static string SerializePlan(ChangePlan plan) =>
        JsonSerializer.Serialize(plan, PlanJsonOptions);

    private static string NormalizeErrorCode(string errorCode) =>
        StableErrorCodes.Contains(errorCode ?? string.Empty)
            ? errorCode!
            : BridgeErrorCodes.InternalError;

    private static McpException ControlledError(string errorCode, string message) =>
        new($"{errorCode}: {message}");
}
