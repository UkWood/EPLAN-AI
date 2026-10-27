using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Transport;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Audeu.EplanAi.Mcp.Tools;

public sealed class EplanReadTools
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
        BridgeErrorCodes.ValidationFailed,
        BridgeErrorCodes.PlanAlreadyApplied,
        BridgeErrorCodes.BridgeUnknownOperation,
        BridgeErrorCodes.InternalError
    };

    [McpServerTool(
        Name = "eplan_status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the status of the local EPLAN AI Bridge and its protocol version.")]
    public static Task<string> StatusAsync(
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken) =>
        ExecuteAsync(bridgeClient, "system.status", null, null, cancellationToken);

    [McpServerTool(
        Name = "eplan_get_active_project",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns compact metadata for the active EPLAN project and opens a read session.")]
    public static Task<string> GetActiveProjectAsync(
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken) =>
        ExecuteAsync(bridgeClient, "project.getActive", null, null, cancellationToken);

    [McpServerTool(
        Name = "eplan_list_pages",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists compact page metadata for an existing EPLAN project session.")]
    public static Task<string> ListPagesAsync(
        [Description("Project session identifier returned by eplan_get_active_project.")]
        Guid projectSessionId,
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(projectSessionId);
        return ExecuteAsync(
            bridgeClient,
            "project.listPages",
            projectSessionId,
            null,
            cancellationToken);
    }

    [McpServerTool(
        Name = "eplan_get_selection",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns compact references for the current EPLAN selection in an existing project session.")]
    public static Task<string> GetSelectionAsync(
        [Description("Project session identifier returned by eplan_get_active_project.")]
        Guid projectSessionId,
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(projectSessionId);
        return ExecuteAsync(
            bridgeClient,
            "selection.get",
            projectSessionId,
            null,
            cancellationToken);
    }

    [McpServerTool(
        Name = "eplan_get_object",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns whitelisted compact metadata for an EPLAN object reference in an existing project session.")]
    public static Task<string> GetObjectAsync(
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
        IBridgeClient bridgeClient,
        CancellationToken cancellationToken)
    {
        ValidateSessionId(projectSessionId);
        if (string.IsNullOrWhiteSpace(stringIdentifier))
        {
            throw ControlledError(
                BridgeErrorCodes.ValidationFailed,
                "stringIdentifier is required.");
        }

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["databaseIdentifier"] = databaseIdentifier.ToString(CultureInfo.InvariantCulture),
            ["typeIdentifier"] = typeIdentifier.ToString(CultureInfo.InvariantCulture),
            ["objectIdentifier"] = objectIdentifier.ToString(CultureInfo.InvariantCulture),
            ["stringIdentifier"] = stringIdentifier
        };

        return ExecuteAsync(
            bridgeClient,
            "object.get",
            projectSessionId,
            arguments,
            cancellationToken);
    }

    private static async Task<string> ExecuteAsync(
        IBridgeClient bridgeClient,
        string operation,
        Guid? projectSessionId,
        Dictionary<string, string>? arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        var request = new BridgeRequest
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = Guid.NewGuid(),
            Operation = operation,
            Mode = "read",
            ProjectSessionId = projectSessionId,
            Arguments = arguments ?? new Dictionary<string, string>()
        };

        BridgeResponse response = await bridgeClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.Success)
        {
            string errorCode = StableErrorCodes.Contains(response.ErrorCode ?? string.Empty)
                ? response.ErrorCode!
                : BridgeErrorCodes.InternalError;
            string message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                ? "EPLAN bridge operation failed."
                : response.ErrorMessage!;
            throw ControlledError(errorCode, message);
        }

        if (string.IsNullOrWhiteSpace(response.DataJson))
        {
            throw ControlledError(
                BridgeErrorCodes.InternalError,
                "EPLAN bridge returned no JSON result.");
        }

        return response.DataJson;
    }

    private static void ValidateSessionId(Guid projectSessionId)
    {
        if (projectSessionId == Guid.Empty)
        {
            throw ControlledError(
                BridgeErrorCodes.ValidationFailed,
                "projectSessionId must be a non-empty GUID.");
        }
    }

    private static McpException ControlledError(string errorCode, string message) =>
        new($"{errorCode}: {message}");
}
