using System;
using System.Linq;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Transport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Audeu.EplanAi.Mcp.Tests;

public sealed class McpHostCompositionTests
{
    [Fact]
    public void CreateBuilder_RegistersFixedBridgeClientAndMcpServerServices()
    {
        var builder = Program.CreateBuilder(Array.Empty<string>());

        using var services = builder.Services.BuildServiceProvider();
        Assert.IsType<NamedPipeBridgeClient>(services.GetRequiredService<IBridgeClient>());
        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType.Namespace?.StartsWith(
                "ModelContextProtocol",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CreateBuilder_RegistersTask10ChangePlanServicesAsSingletons()
    {
        var builder = Program.CreateBuilder(Array.Empty<string>());

        using var services = builder.Services.BuildServiceProvider();
        ChangePlanStore firstStore = services.GetRequiredService<ChangePlanStore>();
        ChangePlanStore secondStore = services.GetRequiredService<ChangePlanStore>();
        FunctionTextChangePlanner firstPlanner = services.GetRequiredService<FunctionTextChangePlanner>();
        FunctionTextChangePlanner secondPlanner = services.GetRequiredService<FunctionTextChangePlanner>();
        TimeProvider clock = services.GetRequiredService<TimeProvider>();

        Assert.Same(firstStore, secondStore);
        Assert.Same(firstPlanner, secondPlanner);
        Assert.Same(TimeProvider.System, clock);
    }

    [Fact]
    public void McpAssembly_DoesNotReferenceAnyEplanApiAssembly()
    {
        string[] references = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name.StartsWith("Eplan.", StringComparison.OrdinalIgnoreCase));
    }
}
