using System;
using System.Threading;
using Audeu.EplanAi.LegacyHost.Diagnostics;
using Audeu.EplanAi.LegacyHost.Hosting;

namespace Audeu.EplanAi.LegacyHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args != null
                && args.Length == 1
                && string.Equals(args[0], "--task6-probe", StringComparison.Ordinal))
            {
                return Task6Probe.RunAsync().GetAwaiter().GetResult();
            }

            using (var cancellation = new CancellationTokenSource())
            using (var host = PersistentLegacyHost.CreateDefault())
            {
                ConsoleCancelEventHandler cancelHandler = (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };

                Console.CancelKeyPress += cancelHandler;
                try
                {
                    var result = host.RunAsync(cancellation.Token).GetAwaiter().GetResult();
                    if (!result.Success)
                    {
                        Console.Error.WriteLine(
                            "FAIL: LegacyHost startup -> " + result.ErrorCode + " " + result.ErrorMessage);
                        return 2;
                    }

                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }
    }
}
