using System;
using System.Threading.Tasks;
using Audeu.EplanAi.Mcp.ChangePlans;
using Audeu.EplanAi.Mcp.Tools;
using Audeu.EplanAi.Mcp.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Audeu.EplanAi.Mcp;

internal static class Program
{
    internal static HostApplicationBuilder CreateBuilder(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddSingleton<IBridgeClient, NamedPipeBridgeClient>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<ChangePlanStore>();
        builder.Services.AddSingleton<FunctionTextChangePlanner>();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<EplanReadTools>();

        return builder;
    }

    private static async Task<int> Main(string[] args)
    {
        HostApplicationBuilder builder = CreateBuilder(args);
        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }
}
