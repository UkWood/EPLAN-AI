using System.Text.Json;

namespace Audeu.EplanAi.McpProbe;

public sealed record GateCChangePlan(
    Guid ChangePlanId,
    Guid ProjectSessionId,
    string Status,
    uint DatabaseIdentifier,
    uint TypeIdentifier,
    long ObjectIdentifier,
    string StringIdentifier,
    string OldValue,
    string NewValue);

public static class GateCProbeContract
{
    public static readonly string[] ExpectedTools =
    [
        "eplan_apply_change_plan",
        "eplan_get_active_project",
        "eplan_get_object",
        "eplan_get_selection",
        "eplan_list_pages",
        "eplan_preview_set_function_text",
        "eplan_status",
        "eplan_undo_last_ai_change"
    ];

    public static GateCChangePlan ParsePreviewPlan(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Guid changePlanId = RequiredGuid(root, "changePlanId");
        Guid projectSessionId = RequiredGuid(root, "projectSessionId");
        string status = RequiredString(root, "status");
        Require(string.Equals(status, "previewed", StringComparison.Ordinal),
            $"Expected previewed plan status, got '{status}'.");

        JsonElement operations = RequiredProperty(root, "operations");
        Require(operations.ValueKind == JsonValueKind.Array, "Change Plan operations must be an array.");
        Require(operations.GetArrayLength() == 1, "Gate C requires exactly one Change Plan operation.");

        JsonElement operation = operations[0];
        Require(string.Equals(RequiredString(operation, "kind"), "setFunctionText", StringComparison.Ordinal),
            "Gate C accepts only setFunctionText Change Plans.");

        JsonElement target = RequiredProperty(operation, "target");
        Require(target.ValueKind == JsonValueKind.Object, "Change Plan target must be an object.");
        Guid targetSession = RequiredGuid(target, "projectSessionId");
        Require(targetSession == projectSessionId, "Change Plan target session does not match the plan session.");

        uint databaseIdentifier = RequiredUInt32(target, "databaseIdentifier");
        uint typeIdentifier = RequiredUInt32(target, "typeIdentifier");
        long objectIdentifier = RequiredInt64(target, "objectIdentifier");
        string stringIdentifier = RequiredString(target, "stringIdentifier");
        Require(!string.IsNullOrWhiteSpace(stringIdentifier), "Change Plan stringIdentifier must not be empty.");

        return new GateCChangePlan(
            changePlanId,
            projectSessionId,
            status,
            databaseIdentifier,
            typeIdentifier,
            objectIdentifier,
            stringIdentifier,
            RequiredString(operation, "oldValue"),
            RequiredString(operation, "newValue"));
    }

    public static void RequireAppliedPlan(string json, Guid expectedPlanId)
    {
        Require(expectedPlanId != Guid.Empty, "Expected Change Plan ID must not be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Guid actualPlanId = RequiredGuid(root, "changePlanId");
        string status = RequiredString(root, "status");
        Require(actualPlanId == expectedPlanId,
            $"Applied Change Plan ID mismatch. Expected {expectedPlanId:D}, got {actualPlanId:D}.");
        Require(string.Equals(status, "applied", StringComparison.Ordinal),
            $"Expected applied plan status, got '{status}'.");
    }

    public static void RequireUndoResult(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        string status = RequiredString(document.RootElement, "status");
        Require(string.Equals(status, "undone", StringComparison.Ordinal),
            $"Expected undo status 'undone', got '{status}'.");
    }

    public static void RequireDisposableProjectConfirmation(
        string actualProjectName,
        string expectedProjectName,
        string confirmedDisposableProjectName)
    {
        Require(!string.IsNullOrWhiteSpace(actualProjectName), "Active project name must not be empty.");
        Require(!string.IsNullOrWhiteSpace(expectedProjectName), "Expected project name must not be empty.");
        Require(!string.IsNullOrWhiteSpace(confirmedDisposableProjectName),
            "Explicit disposable project confirmation must not be empty.");
        Require(string.Equals(actualProjectName, expectedProjectName, StringComparison.Ordinal),
            $"Active project '{actualProjectName}' does not match expected project '{expectedProjectName}'.");
        Require(string.Equals(actualProjectName, confirmedDisposableProjectName, StringComparison.Ordinal),
            "Gate C mutation refused: explicit disposable project confirmation does not exactly match the active project.");
    }

    private static JsonElement RequiredProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidOperationException($"JSON result is missing required property '{name}'.");

    private static string RequiredString(JsonElement element, string name)
    {
        JsonElement value = RequiredProperty(element, name);
        Require(value.ValueKind == JsonValueKind.String, $"JSON property '{name}' is not a string.");
        return value.GetString() ?? throw new InvalidOperationException($"JSON property '{name}' is null.");
    }

    private static Guid RequiredGuid(JsonElement element, string name)
    {
        string text = RequiredString(element, name);
        Require(Guid.TryParse(text, out Guid value) && value != Guid.Empty,
            $"JSON property '{name}' is not a non-empty GUID: '{text}'.");
        return value;
    }

    private static uint RequiredUInt32(JsonElement element, string name)
    {
        JsonElement value = RequiredProperty(element, name);
        Require(value.TryGetUInt32(out uint result), $"JSON property '{name}' is not a UInt32.");
        return result;
    }

    private static long RequiredInt64(JsonElement element, string name)
    {
        JsonElement value = RequiredProperty(element, name);
        Require(value.TryGetInt64(out long result), $"JSON property '{name}' is not an Int64.");
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
