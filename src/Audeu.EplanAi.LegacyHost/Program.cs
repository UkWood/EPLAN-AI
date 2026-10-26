using System;
using Audeu.EplanAi.LegacyHost.Diagnostics;

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

            // The persistent named-pipe host is introduced in Task 7.
            return 0;
        }
    }
}
