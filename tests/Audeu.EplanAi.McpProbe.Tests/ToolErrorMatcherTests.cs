using Xunit;

namespace Audeu.EplanAi.McpProbe.Tests;

public sealed class ToolErrorMatcherTests
{
    [Theory]
    [InlineData("NO_ACTIVE_PROJECT: No active EPLAN project is available.", "NO_ACTIVE_PROJECT")]
    [InlineData("An error occurred invoking 'eplan_list_pages': NO_ACTIVE_PROJECT: No active EPLAN project is available.", "NO_ACTIVE_PROJECT")]
    [InlineData("An error occurred invoking 'eplan_get_object': SESSION_STALE: The project session is stale.", "SESSION_STALE")]
    public void MatchesStableCodeInRawOrSdkWrappedToolError(string text, string code)
    {
        Assert.True(ToolErrorMatcher.Matches(text, code));
    }

    [Theory]
    [InlineData("NO_ACTIVE_PROJECTING: wrong code", "NO_ACTIVE_PROJECT")]
    [InlineData("Some detail mentions NO_ACTIVE_PROJECT but has no code delimiter", "NO_ACTIVE_PROJECT")]
    [InlineData("An error occurred invoking 'eplan_list_pages': SESSION_STALE: stale", "NO_ACTIVE_PROJECT")]
    public void RejectsUnrelatedOrPartialMatches(string text, string code)
    {
        Assert.False(ToolErrorMatcher.Matches(text, code));
    }
}
