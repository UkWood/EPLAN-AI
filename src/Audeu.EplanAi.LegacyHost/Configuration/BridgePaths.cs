using System;
using System.IO;

namespace Audeu.EplanAi.LegacyHost.Configuration
{
    public sealed class BridgePaths
    {
        public BridgePaths(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("A spool root path is required.", nameof(rootPath));
            }

            RootPath = Path.GetFullPath(rootPath);
            RequestDirectory = Path.Combine(RootPath, "requests");
            ResponseDirectory = Path.Combine(RootPath, "responses");
        }

        public string RootPath { get; }

        public string RequestDirectory { get; }

        public string ResponseDirectory { get; }

        public static BridgePaths CreateDefault()
        {
            return new BridgePaths(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Audeu",
                "EplanAiBridge"));
        }

        public string GetRequestPath(Guid requestId)
        {
            return Path.Combine(RequestDirectory, requestId.ToString("D") + ".request.json");
        }

        public string GetResponsePath(Guid requestId)
        {
            return Path.Combine(ResponseDirectory, requestId.ToString("D") + ".response.json");
        }
    }
}
