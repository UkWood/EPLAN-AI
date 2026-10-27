using System;
using Xunit;

namespace Audeu.EplanAi.McpProbe.Tests;

public sealed class GateCProbeOptionsTests
{
    [Fact]
    public void Parse_ValidGateCArguments_RequiresExactDisposableConfirmation()
    {
        GateCProbeOptions options = GateCProbeOptions.Parse(
            new[]
            {
                "--server", "C:\\probe\\Audeu.EplanAi.Mcp.exe",
                "--expected-project-name", "Gate C Scratch",
                "--confirmed-disposable-project-name", "Gate C Scratch",
                "--temporary-function-text", "Audeu Gate C temporary Function Text"
            },
            _ => true);

        Assert.Equal("C:\\probe\\Audeu.EplanAi.Mcp.exe", options.ServerPath);
        Assert.Equal("Gate C Scratch", options.ExpectedProjectName);
        Assert.Equal("Gate C Scratch", options.ConfirmedDisposableProjectName);
        Assert.Equal("Audeu Gate C temporary Function Text", options.TemporaryFunctionText);
    }

    [Fact]
    public void Parse_MismatchedDisposableConfirmation_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() => GateCProbeOptions.Parse(
            new[]
            {
                "--server", "C:\\probe\\Audeu.EplanAi.Mcp.exe",
                "--expected-project-name", "Gate C Scratch",
                "--confirmed-disposable-project-name", "Production",
                "--temporary-function-text", "Temporary"
            },
            _ => true));
    }

    [Fact]
    public void Parse_MissingDisposableConfirmation_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() => GateCProbeOptions.Parse(
            new[]
            {
                "--server", "C:\\probe\\Audeu.EplanAi.Mcp.exe",
                "--expected-project-name", "Gate C Scratch",
                "--temporary-function-text", "Temporary"
            },
            _ => true));
    }

    [Fact]
    public void Parse_EmptyTemporaryText_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() => GateCProbeOptions.Parse(
            new[]
            {
                "--server", "C:\\probe\\Audeu.EplanAi.Mcp.exe",
                "--expected-project-name", "Gate C Scratch",
                "--confirmed-disposable-project-name", "Gate C Scratch",
                "--temporary-function-text", ""
            },
            _ => true));
    }

    [Fact]
    public void Parse_MissingServerBinary_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() => GateCProbeOptions.Parse(
            new[]
            {
                "--server", "C:\\probe\\missing.exe",
                "--expected-project-name", "Gate C Scratch",
                "--confirmed-disposable-project-name", "Gate C Scratch",
                "--temporary-function-text", "Temporary"
            },
            _ => false));
    }
}
