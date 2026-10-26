using System;
using System.IO;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class RuntimeDeploymentTests
    {
        [Fact]
        public void LegacyHost_ProjectCopiesEplanRemoteRuntimeAssemblies()
        {
            var root = FindRepositoryRoot();
            var projectPath = Path.Combine(root, "src", "Audeu.EplanAi.LegacyHost", "Audeu.EplanAi.LegacyHost.csproj");
            var xml = File.ReadAllText(projectPath);

            Assert.Contains("Eplan.EplApi.RemoteClientu\" HintPath=\"$(EplanApi29Dir)\\Eplan.EplApi.RemoteClientu.dll\" Private=\"true\"", xml, StringComparison.Ordinal);
            Assert.Contains("Eplan.EplApi.Remotingu\" HintPath=\"$(EplanApi29Dir)\\Eplan.EplApi.Remotingu.dll\" Private=\"true\"", xml, StringComparison.Ordinal);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                    && Directory.Exists(Path.Combine(directory.FullName, "tests")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root could not be located from the test output directory.");
        }
    }
}
