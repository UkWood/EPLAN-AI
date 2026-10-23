using System;

namespace Audeu.EplanAi.Contracts
{
    public static class ProtocolConstants
    {
        public const string Version = "1.0";

        public static bool IsSupported(string version)
        {
            return string.Equals(version, Version, StringComparison.Ordinal);
        }
    }
}
