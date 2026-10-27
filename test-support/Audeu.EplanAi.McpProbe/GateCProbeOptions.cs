namespace Audeu.EplanAi.McpProbe;

public sealed record GateCProbeOptions(
    string ServerPath,
    string ExpectedProjectName,
    string ConfirmedDisposableProjectName,
    string TemporaryFunctionText)
{
    public static GateCProbeOptions Parse(string[] args)
    {
        return Parse(args, File.Exists);
    }

    public static GateCProbeOptions Parse(string[] args, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(fileExists);

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
        string expectedProject = Required(values, "--expected-project-name");
        string confirmedDisposableProject = Required(values, "--confirmed-disposable-project-name");
        string temporaryFunctionText = RequiredAllowingSpaces(values, "--temporary-function-text");

        if (!string.Equals(expectedProject, confirmedDisposableProject, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Gate C mutation refused: --confirmed-disposable-project-name must exactly match --expected-project-name.");
        }

        if (string.IsNullOrEmpty(temporaryFunctionText))
        {
            throw new ArgumentException("--temporary-function-text must not be empty.");
        }

        if (!fileExists(server))
        {
            throw new ArgumentException($"MCP server executable does not exist: {server}");
        }

        return new GateCProbeOptions(
            server,
            expectedProject,
            confirmedDisposableProject,
            temporaryFunctionText);
    }

    private static string Required(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw Usage();

    private static string RequiredAllowingSpaces(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out string? value)
            ? value
            : throw Usage();

    private static ArgumentException Usage() => new(
        "Usage: --server <path> --expected-project-name <name> --confirmed-disposable-project-name <same-name> --temporary-function-text <text>");
}
