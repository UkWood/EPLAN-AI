namespace Audeu.EplanAi.McpProbe;

public static class ToolErrorMatcher
{
    public static bool Matches(string text, string expectedCode)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCode);

        string expectedPrefix = expectedCode.EndsWith(':')
            ? expectedCode
            : expectedCode + ":";

        if (text.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        return text.Contains(": " + expectedPrefix, StringComparison.Ordinal);
    }
}
