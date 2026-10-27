using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.Mcp.Transport;

namespace Audeu.EplanAi.Mcp.ChangePlans;

public sealed class FunctionTextChangePlanner
{
    public const int MaxFunctionTextLength = 1024;

    private readonly IBridgeClient _bridge;
    private readonly ChangePlanStore _store;
    private readonly TimeProvider _timeProvider;

    public FunctionTextChangePlanner(
        IBridgeClient bridge,
        ChangePlanStore store,
        TimeProvider timeProvider)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ChangePlan> PreviewAsync(
        ObjectReference target,
        string newText,
        CancellationToken cancellationToken)
    {
        ObjectReference snapshot = SnapshotTarget(target);
        ValidatePreview(snapshot, newText);

        var request = new BridgeRequest
        {
            ProtocolVersion = ProtocolConstants.Version,
            RequestId = Guid.NewGuid(),
            Operation = "object.get",
            Mode = "read",
            ProjectSessionId = snapshot.ProjectSessionId,
            Arguments = new Dictionary<string, string>
            {
                ["databaseIdentifier"] = snapshot.DatabaseIdentifier.ToString(CultureInfo.InvariantCulture),
                ["typeIdentifier"] = snapshot.TypeIdentifier.ToString(CultureInfo.InvariantCulture),
                ["objectIdentifier"] = snapshot.ObjectIdentifier.ToString(CultureInfo.InvariantCulture),
                ["stringIdentifier"] = snapshot.StringIdentifier
            }
        };

        BridgeResponse response = await _bridge.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            throw Error(BridgeErrorCodes.InternalError, "The EPLAN bridge returned no response.");
        }

        if (!response.Success)
        {
            if (IsKnownBridgeError(response.ErrorCode))
            {
                string message = string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? "The EPLAN bridge rejected the preview read."
                    : response.ErrorMessage;
                throw Error(response.ErrorCode, message);
            }

            throw Error(BridgeErrorCodes.InternalError, "The EPLAN bridge returned an unknown error code.");
        }

        string oldText = ParseFunctionText(response.DataJson);
        var operation = new ChangeOperation("setFunctionText", snapshot, oldText, newText);
        var plan = new ChangePlan(
            Guid.NewGuid(),
            _timeProvider.GetUtcNow().UtcDateTime,
            snapshot.ProjectSessionId,
            new[] { operation });

        _store.Add(plan);
        return plan;
    }

    private static ObjectReference SnapshotTarget(ObjectReference target)
    {
        if (target is null)
        {
            throw Error(BridgeErrorCodes.ValidationFailed, "A Function target is required.");
        }

        return new ObjectReference
        {
            ProjectSessionId = target.ProjectSessionId,
            DatabaseIdentifier = target.DatabaseIdentifier,
            TypeIdentifier = target.TypeIdentifier,
            ObjectIdentifier = target.ObjectIdentifier,
            StringIdentifier = target.StringIdentifier
        };
    }

    private static void ValidatePreview(ObjectReference target, string newText)
    {
        if (target.ProjectSessionId == Guid.Empty)
        {
            throw Error(BridgeErrorCodes.ValidationFailed, "A non-empty project session ID is required.");
        }

        if (string.IsNullOrWhiteSpace(target.StringIdentifier))
        {
            throw Error(BridgeErrorCodes.ValidationFailed, "A compact Function string identifier is required.");
        }

        if (newText is null)
        {
            throw Error(BridgeErrorCodes.ValidationFailed, "Function Text cannot be null.");
        }

        if (newText.Length > MaxFunctionTextLength)
        {
            throw Error(
                BridgeErrorCodes.ValidationFailed,
                $"Function Text must not exceed {MaxFunctionTextLength} UTF-16 code units.");
        }
    }

    private static string ParseFunctionText(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            throw Error(BridgeErrorCodes.InternalError, "The EPLAN object response contained no data.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(dataJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("kind", out JsonElement kind)
                || kind.ValueKind != JsonValueKind.String
                || !string.Equals(kind.GetString(), "Function", StringComparison.Ordinal)
                || !root.TryGetProperty("functionText", out JsonElement functionText))
            {
                throw Error(
                    BridgeErrorCodes.InternalError,
                    "The EPLAN object response was not a valid Function snapshot.");
            }

            return functionText.ValueKind switch
            {
                JsonValueKind.Null => string.Empty,
                JsonValueKind.String => functionText.GetString() ?? string.Empty,
                _ => throw Error(
                    BridgeErrorCodes.InternalError,
                    "The EPLAN Function Text value had an unexpected JSON type.")
            };
        }
        catch (JsonException)
        {
            throw Error(BridgeErrorCodes.InternalError, "The EPLAN object response was not valid compact JSON.");
        }
    }

    private static bool IsKnownBridgeError(string errorCode)
    {
        return errorCode == BridgeErrorCodes.ProtocolMismatch
            || errorCode == BridgeErrorCodes.EplanNotFound
            || errorCode == BridgeErrorCodes.EplanNotConnected
            || errorCode == BridgeErrorCodes.EplanActionFailed
            || errorCode == BridgeErrorCodes.OperationTimeout
            || errorCode == BridgeErrorCodes.NoActiveProject
            || errorCode == BridgeErrorCodes.SessionStale
            || errorCode == BridgeErrorCodes.ObjectNotFound
            || errorCode == BridgeErrorCodes.ObjectInvalid
            || errorCode == BridgeErrorCodes.ObjectTypeNotAllowed
            || errorCode == BridgeErrorCodes.ValidationFailed
            || errorCode == BridgeErrorCodes.BridgeUnknownOperation
            || errorCode == BridgeErrorCodes.InternalError;
    }

    private static ChangePlanException Error(string errorCode, string message)
    {
        return new ChangePlanException(errorCode, message);
    }
}
