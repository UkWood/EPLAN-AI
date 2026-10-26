using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Audeu.EplanAi.McpProbe;

internal static class Program
{
    private static readonly string[] ExpectedTools =
    [
        "eplan_get_active_project",
        "eplan_get_object",
        "eplan_get_selection",
        "eplan_list_pages",
        "eplan_status"
    ];

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            await RunAsync(options).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL: {ex.Message}");
            return 1;
        }
    }

    private static async Task RunAsync(Options options)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Audeu EPLAN Task 9 Probe",
            Command = options.ServerPath,
            WorkingDirectory = Path.GetDirectoryName(options.ServerPath),
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        });

        await using McpClient client = await McpClient.CreateAsync(transport).ConfigureAwait(false);

        IList<McpClientTool> tools = await client.ListToolsAsync().ConfigureAwait(false);
        string[] actualTools = tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Require(actualTools.SequenceEqual(ExpectedTools, StringComparer.Ordinal),
            $"MCP tool list mismatch. Expected [{string.Join(", ", ExpectedTools)}], got [{string.Join(", ", actualTools)}].");
        Pass("MCP discovery exposes exactly five approved read-only tools");

        string statusJson = await CallTextAsync(client, "eplan_status").ConfigureAwait(false);
        using (JsonDocument statusDocument = JsonDocument.Parse(statusJson))
        {
            JsonElement status = statusDocument.RootElement;
            Require(GetString(status, "status") == "ready", "eplan_status did not report status=ready.");
            Require(GetString(status, "protocolVersion") == "1.0", "eplan_status did not report protocolVersion=1.0.");
        }
        Pass("eplan_status through real MCP stdio client (status=ready, protocol=1.0)");

        ActiveProject project = await GetActiveProjectAsync(client).ConfigureAwait(false);
        ValidateExpectedProject(project, options);
        Pass($"eplan_get_active_project (name={project.Name}, pages={project.PageCount}, session={project.ProjectSessionId:D})");

        string pagesJson = await CallTextAsync(
            client,
            "eplan_list_pages",
            new Dictionary<string, object?> { ["projectSessionId"] = project.ProjectSessionId }).ConfigureAwait(false);
        using (JsonDocument pagesDocument = JsonDocument.Parse(pagesJson))
        {
            JsonElement root = pagesDocument.RootElement;
            Require(GetGuid(root, "projectSessionId") == project.ProjectSessionId, "Page list returned a different projectSessionId.");
            JsonElement items = GetArray(root, "items");
            Require(items.GetArrayLength() == options.ExpectedPageCount,
                $"Expected {options.ExpectedPageCount} pages but MCP returned {items.GetArrayLength()}.");
        }
        Pass($"eplan_list_pages through MCP (items={options.ExpectedPageCount})");

        string selectionJson = await CallTextAsync(
            client,
            "eplan_get_selection",
            new Dictionary<string, object?> { ["projectSessionId"] = project.ProjectSessionId }).ConfigureAwait(false);
        ObjectReference oldReference;
        string selectedName;
        using (JsonDocument selectionDocument = JsonDocument.Parse(selectionJson))
        {
            JsonElement root = selectionDocument.RootElement;
            Require(GetGuid(root, "projectSessionId") == project.ProjectSessionId, "Selection returned a different projectSessionId.");
            JsonElement items = GetArray(root, "items");
            Require(items.GetArrayLength() == 1, $"Expected exactly one selected Function but got {items.GetArrayLength()} selection items.");
            JsonElement selected = items[0];
            Require(GetString(selected, "kind") == "Function", "The selected EPLAN object is not a Function.");
            selectedName = GetString(selected, "name");
            oldReference = ParseReference(GetObject(selected, "reference"));
            Require(oldReference.ProjectSessionId == project.ProjectSessionId, "Selected Function reference belongs to a different session.");
        }
        Pass($"eplan_get_selection through MCP (Function: {selectedName})");

        string functionJson = await CallTextAsync(
            client,
            "eplan_get_object",
            ObjectArguments(oldReference)).ConfigureAwait(false);
        using (JsonDocument functionDocument = JsonDocument.Parse(functionJson))
        {
            JsonElement function = functionDocument.RootElement;
            Require(GetString(function, "kind") == "Function", "eplan_get_object did not return kind=Function.");
            string functionText = GetString(function, "functionText");
            Require(string.Equals(functionText, options.ExpectedFunctionText, StringComparison.Ordinal),
                $"Function Text mismatch. Expected '{options.ExpectedFunctionText}', got '{functionText}'.");
        }
        Pass($"eplan_get_object through MCP; Function Text exactly matches '{options.ExpectedFunctionText}'");

        Console.WriteLine();
        Console.WriteLine("ACTION REQUIRED: close the active project in EPLAN so that no project is active.");
        Console.WriteLine("When the project is fully closed, press Enter here.");
        Console.ReadLine();

        await ExpectToolErrorAsync(
            client,
            "eplan_list_pages",
            new Dictionary<string, object?> { ["projectSessionId"] = project.ProjectSessionId },
            "NO_ACTIVE_PROJECT:").ConfigureAwait(false);
        Pass("closed-project MCP read returned NO_ACTIVE_PROJECT and cleared the old project session");

        Console.WriteLine();
        Console.WriteLine($"ACTION REQUIRED: reopen the safe project '{options.ExpectedProjectName}' in EPLAN.");
        Console.WriteLine("When it is fully active again, press Enter here.");
        Console.ReadLine();

        ActiveProject reopened = await GetActiveProjectAsync(client).ConfigureAwait(false);
        ValidateExpectedProject(reopened, options);
        Require(reopened.ProjectSessionId != project.ProjectSessionId,
            "Reopened project unexpectedly reused the cleared projectSessionId.");
        Pass($"reopened project established fresh session {reopened.ProjectSessionId:D}");

        await ExpectToolErrorAsync(
            client,
            "eplan_get_object",
            ObjectArguments(oldReference),
            "SESSION_STALE:").ConfigureAwait(false);
        Pass("old Function reference rejected through MCP with SESSION_STALE");

        Console.WriteLine();
        Console.WriteLine("PASS: Gate B real MCP-to-EPLAN read-only acceptance completed successfully.");
    }

    private static async Task<ActiveProject> GetActiveProjectAsync(McpClient client)
    {
        string json = await CallTextAsync(client, "eplan_get_active_project").ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ActiveProject(
            GetGuid(root, "projectSessionId"),
            GetUInt32(root, "databaseIdentifier"),
            GetString(root, "name"),
            GetInt32(root, "pageCount"));
    }

    private static void ValidateExpectedProject(ActiveProject project, Options options)
    {
        Require(project.ProjectSessionId != Guid.Empty, "Active project returned an empty projectSessionId.");
        Require(string.Equals(project.Name, options.ExpectedProjectName, StringComparison.Ordinal),
            $"Expected project '{options.ExpectedProjectName}' but MCP returned '{project.Name}'.");
        Require(project.PageCount == options.ExpectedPageCount,
            $"Expected project pageCount={options.ExpectedPageCount} but MCP returned {project.PageCount}.");
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
        Require(result.IsError is true, $"{toolName} unexpectedly succeeded; expected MCP tool error {expectedPrefix}");
        Require(text.StartsWith(expectedPrefix, StringComparison.Ordinal),
            $"{toolName} returned unexpected MCP error. Expected prefix '{expectedPrefix}', got '{text}'.");
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
        return Guid.TryParse(text, out Guid value)
            ? value
            : throw new InvalidOperationException($"JSON property '{name}' is not a GUID: '{text}'.");
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

    private static int GetInt32(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        return value.TryGetInt32(out int result)
            ? result
            : throw new InvalidOperationException($"JSON property '{name}' is not an Int32.");
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

    private sealed record ActiveProject(Guid ProjectSessionId, uint DatabaseIdentifier, string Name, int PageCount);

    private sealed record ObjectReference(
        Guid ProjectSessionId,
        uint DatabaseIdentifier,
        uint TypeIdentifier,
        long ObjectIdentifier,
        string StringIdentifier);

    private sealed record Options(
        string ServerPath,
        string ExpectedProjectName,
        int ExpectedPageCount,
        string ExpectedFunctionText)
    {
        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    throw Usage();
                }

                values[args[i]] = args[i + 1];
            }

            string server = Required(values, "--server");
            string project = Required(values, "--expected-project-name");
            string pageCountText = Required(values, "--expected-page-count");
            string functionText = Required(values, "--expected-function-text");

            string fullServerPath = Path.GetFullPath(server);
            if (!File.Exists(fullServerPath))
            {
                throw new ArgumentException($"MCP server executable does not exist: {fullServerPath}");
            }

            if (!int.TryParse(pageCountText, NumberStyles.None, CultureInfo.InvariantCulture, out int pageCount) || pageCount <= 0)
            {
                throw new ArgumentException("--expected-page-count must be a positive integer.");
            }

            if (string.IsNullOrEmpty(functionText))
            {
                throw new ArgumentException("--expected-function-text must not be empty; select a Function with known text for Gate B.");
            }

            return new Options(fullServerPath, project, pageCount, functionText);
        }

        private static string Required(Dictionary<string, string> values, string name) =>
            values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw Usage();

        private static ArgumentException Usage() => new(
            "Usage: Audeu.EplanAi.McpProbe --server <path> --expected-project-name <name> --expected-page-count <count> --expected-function-text <text>");
    }
}
