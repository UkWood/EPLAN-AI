using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Audeu.EplanAi.McpProbe;

internal static class GateCProbeRunner
{
    private const string FailureMarkerFileName = "Audeu.EplanAi.GateC.fail-next-write";

    public static async Task RunAsync(GateCProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Audeu EPLAN Task 12 Gate C Probe",
            Command = options.ServerPath,
            WorkingDirectory = Path.GetDirectoryName(options.ServerPath),
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        });

        await using McpClient client = await McpClient.CreateAsync(transport).ConfigureAwait(false);

        IList<McpClientTool> tools = await client.ListToolsAsync().ConfigureAwait(false);
        string[] actualTools = tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Require(actualTools.SequenceEqual(GateCProbeContract.ExpectedTools, StringComparer.Ordinal),
            $"MCP tool list mismatch. Expected [{string.Join(", ", GateCProbeContract.ExpectedTools)}], got [{string.Join(", ", actualTools)}].");
        Pass("MCP discovery exposes exactly eight approved Task 12 tools");

        string statusJson = await CallTextAsync(client, "eplan_status").ConfigureAwait(false);
        using (JsonDocument statusDocument = JsonDocument.Parse(statusJson))
        {
            JsonElement status = statusDocument.RootElement;
            Require(GetString(status, "status") == "ready", "eplan_status did not report status=ready.");
            Require(GetString(status, "protocolVersion") == "1.0", "eplan_status did not report protocolVersion=1.0.");
        }
        Pass("eplan_status through real MCP stdio client");

        ActiveProject project = await GetActiveProjectAsync(client).ConfigureAwait(false);
        GateCProbeContract.RequireDisposableProjectConfirmation(
            project.Name,
            options.ExpectedProjectName,
            options.ConfirmedDisposableProjectName);
        Require(project.ProjectSessionId != Guid.Empty, "Active project returned an empty projectSessionId.");
        Pass($"disposable project guard accepted '{project.Name}' (session={project.ProjectSessionId:D})");

        ObjectReference target = await GetSingleSelectedFunctionAsync(client, project.ProjectSessionId).ConfigureAwait(false);
        string originalText = await GetFunctionTextAsync(client, target).ConfigureAwait(false);
        Pass($"captured original Function Text: '{originalText}'");

        Require(!string.Equals(originalText, options.TemporaryFunctionText, StringComparison.Ordinal),
            "Temporary Function Text must differ from the current Function Text.");

        GateCChangePlan plan = await PreviewAsync(client, target, options.TemporaryFunctionText).ConfigureAwait(false);
        Require(plan.ProjectSessionId == project.ProjectSessionId, "Preview plan returned a different project session.");
        Require(plan.DatabaseIdentifier == target.DatabaseIdentifier, "Preview plan database identifier changed.");
        Require(plan.TypeIdentifier == target.TypeIdentifier, "Preview plan type identifier changed.");
        Require(plan.ObjectIdentifier == target.ObjectIdentifier, "Preview plan object identifier changed.");
        Require(string.Equals(plan.StringIdentifier, target.StringIdentifier, StringComparison.Ordinal),
            "Preview plan string identifier changed.");
        Require(string.Equals(plan.OldValue, originalText, StringComparison.Ordinal),
            "Preview plan oldValue does not match the live Function Text.");
        Require(string.Equals(plan.NewValue, options.TemporaryFunctionText, StringComparison.Ordinal),
            "Preview plan newValue does not match the requested temporary text.");
        Pass($"preview created immutable plan {plan.ChangePlanId:D}");

        string afterPreview = await GetFunctionTextAsync(client, target).ConfigureAwait(false);
        Require(string.Equals(afterPreview, originalText, StringComparison.Ordinal),
            "Preview unexpectedly changed EPLAN Function Text.");
        Pass("preview is EPLAN-read-only; original Function Text is unchanged");

        string appliedJson = await CallTextAsync(
            client,
            "eplan_apply_change_plan",
            new Dictionary<string, object?> { ["changePlanId"] = plan.ChangePlanId }).ConfigureAwait(false);
        GateCProbeContract.RequireAppliedPlan(appliedJson, plan.ChangePlanId);

        string afterApply = await GetFunctionTextAsync(client, target).ConfigureAwait(false);
        Require(string.Equals(afterApply, options.TemporaryFunctionText, StringComparison.Ordinal),
            "Applied Function Text was not visible through MCP read-back.");
        Pass("apply by changePlanId changed exactly the previewed Function Text");

        string undoJson = await CallTextAsync(
            client,
            "eplan_undo_last_ai_change",
            new Dictionary<string, object?> { ["projectSessionId"] = project.ProjectSessionId }).ConfigureAwait(false);
        GateCProbeContract.RequireUndoResult(undoJson);

        string afterUndo = await GetFunctionTextAsync(client, target).ConfigureAwait(false);
        Require(string.Equals(afterUndo, originalText, StringComparison.Ordinal),
            "Undo did not restore the exact original Function Text.");
        Pass("undo restored the exact original Function Text");

        string rollbackText = "Audeu Gate C rollback " + Guid.NewGuid().ToString("N");
        GateCChangePlan rollbackPlan = await PreviewAsync(client, target, rollbackText).ConfigureAwait(false);
        Require(string.Equals(rollbackPlan.OldValue, originalText, StringComparison.Ordinal),
            "Rollback preview did not start from the restored original Function Text.");

        string markerPath = Path.Combine(Path.GetTempPath(), FailureMarkerFileName);
        try
        {
            File.WriteAllText(markerPath, "1");
            await ExpectToolErrorAsync(
                client,
                "eplan_apply_change_plan",
                new Dictionary<string, object?> { ["changePlanId"] = rollbackPlan.ChangePlanId },
                "EPLAN_ACTION_FAILED:").ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }

        string afterRollbackFailure = await GetFunctionTextAsync(client, target).ConfigureAwait(false);
        Require(string.Equals(afterRollbackFailure, originalText, StringComparison.Ordinal),
            "Controlled pre-commit failure did not roll back to the exact original Function Text.");
        Pass("controlled pre-commit failure rolled back and preserved the original Function Text");

        Console.WriteLine();
        Console.WriteLine("PASS: Gate C real MCP-to-EPLAN preview/apply/undo/rollback acceptance completed successfully.");
    }

    private static async Task<GateCChangePlan> PreviewAsync(
        McpClient client,
        ObjectReference target,
        string newText)
    {
        string json = await CallTextAsync(
            client,
            "eplan_preview_set_function_text",
            new Dictionary<string, object?>
            {
                ["projectSessionId"] = target.ProjectSessionId,
                ["databaseIdentifier"] = target.DatabaseIdentifier,
                ["typeIdentifier"] = target.TypeIdentifier,
                ["objectIdentifier"] = target.ObjectIdentifier,
                ["stringIdentifier"] = target.StringIdentifier,
                ["newText"] = newText
            }).ConfigureAwait(false);

        return GateCProbeContract.ParsePreviewPlan(json);
    }

    private static async Task<ActiveProject> GetActiveProjectAsync(McpClient client)
    {
        string json = await CallTextAsync(client, "eplan_get_active_project").ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ActiveProject(
            GetGuid(root, "projectSessionId"),
            GetString(root, "name"));
    }

    private static async Task<ObjectReference> GetSingleSelectedFunctionAsync(McpClient client, Guid projectSessionId)
    {
        string json = await CallTextAsync(
            client,
            "eplan_get_selection",
            new Dictionary<string, object?> { ["projectSessionId"] = projectSessionId }).ConfigureAwait(false);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Require(GetGuid(root, "projectSessionId") == projectSessionId,
            "Selection returned a different projectSessionId.");
        JsonElement items = GetArray(root, "items");
        Require(items.GetArrayLength() == 1,
            $"Expected exactly one selected Function but got {items.GetArrayLength()} selection items.");

        JsonElement selected = items[0];
        Require(GetString(selected, "kind") == "Function", "The selected EPLAN object is not a Function.");
        ObjectReference reference = ParseReference(GetObject(selected, "reference"));
        Require(reference.ProjectSessionId == projectSessionId,
            "Selected Function reference belongs to a different session.");
        Pass($"selected Function: {GetString(selected, "name")}");
        return reference;
    }

    private static async Task<string> GetFunctionTextAsync(McpClient client, ObjectReference reference)
    {
        string json = await CallTextAsync(client, "eplan_get_object", ObjectArguments(reference)).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement function = document.RootElement;
        Require(GetString(function, "kind") == "Function", "eplan_get_object did not return kind=Function.");
        return GetString(function, "functionText");
    }

    private static async Task<string> CallTextAsync(
        McpClient client,
        string toolName,
        Dictionary<string, object?>? arguments = null)
    {
        CallToolResult result = await client.CallToolAsync(
            toolName,
            arguments ?? new Dictionary<string, object?>(),
            cancellationToken: CancellationToken.None).ConfigureAwait(false);

        string text = SingleText(result);
        if (result.IsError is true)
        {
            throw new InvalidOperationException($"{toolName} returned MCP tool error: {text}");
        }

        return text;
    }

    private static async Task ExpectToolErrorAsync(
        McpClient client,
        string toolName,
        Dictionary<string, object?> arguments,
        string expectedPrefix)
    {
        CallToolResult result = await client.CallToolAsync(
            toolName,
            arguments,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);

        string text = SingleText(result);
        Require(result.IsError is true,
            $"{toolName} unexpectedly succeeded; expected MCP tool error {expectedPrefix}");
        Require(ToolErrorMatcher.Matches(text, expectedPrefix),
            $"{toolName} returned unexpected MCP error. Expected stable error '{expectedPrefix}', got '{text}'.");
    }

    private static string SingleText(CallToolResult result)
    {
        TextContentBlock[] textBlocks = result.Content.OfType<TextContentBlock>().ToArray();
        Require(textBlocks.Length == 1, $"Expected exactly one text content block, got {textBlocks.Length}.");
        return textBlocks[0].Text ?? throw new InvalidOperationException("MCP text content was null.");
    }

    private static Dictionary<string, object?> ObjectArguments(ObjectReference reference) => new()
    {
        ["projectSessionId"] = reference.ProjectSessionId,
        ["databaseIdentifier"] = reference.DatabaseIdentifier,
        ["typeIdentifier"] = reference.TypeIdentifier,
        ["objectIdentifier"] = reference.ObjectIdentifier,
        ["stringIdentifier"] = reference.StringIdentifier
    };

    private static ObjectReference ParseReference(JsonElement reference) => new(
        GetGuid(reference, "projectSessionId"),
        GetUInt32(reference, "databaseIdentifier"),
        GetUInt32(reference, "typeIdentifier"),
        GetInt64(reference, "objectIdentifier"),
        GetString(reference, "stringIdentifier"));

    private static JsonElement GetArray(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        Require(value.ValueKind == JsonValueKind.Array, $"JSON property '{name}' is not an array.");
        return value;
    }

    private static JsonElement GetObject(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        Require(value.ValueKind == JsonValueKind.Object, $"JSON property '{name}' is not an object.");
        return value;
    }

    private static string GetString(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        string? text = value.GetString();
        return text ?? throw new InvalidOperationException($"JSON property '{name}' is null.");
    }

    private static Guid GetGuid(JsonElement element, string name)
    {
        string text = GetString(element, name);
        return Guid.TryParse(text, out Guid value) && value != Guid.Empty
            ? value
            : throw new InvalidOperationException($"JSON property '{name}' is not a non-empty GUID: '{text}'.");
    }

    private static uint GetUInt32(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        return value.TryGetUInt32(out uint result)
            ? result
            : throw new InvalidOperationException($"JSON property '{name}' is not a UInt32.");
    }

    private static long GetInt64(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        return value.TryGetInt64(out long result)
            ? result
            : throw new InvalidOperationException($"JSON property '{name}' is not an Int64.");
    }

    private static JsonElement GetProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidOperationException($"JSON result is missing required property '{name}'.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Pass(string message) => Console.WriteLine($"PASS: {message}");

    private sealed record ActiveProject(Guid ProjectSessionId, string Name);

    private sealed record ObjectReference(
        Guid ProjectSessionId,
        uint DatabaseIdentifier,
        uint TypeIdentifier,
        long ObjectIdentifier,
        string StringIdentifier);
}
