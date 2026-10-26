using System;
using System.IO;
using Eplan.EplApi.ApplicationFramework;

namespace Audeu.EplanAi.AddIn
{
    public sealed class EplanAddIn : IEplAddIn
    {
        private static readonly string DiagnosticsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Audeu",
            "EplanAiBridge",
            "diagnostics");

        private static readonly string LifecycleLogPath = Path.Combine(
            DiagnosticsDirectory,
            "addin-lifecycle.log");

        public bool OnRegister(ref bool bLoadOnStart)
        {
            WriteLifecycle("OnRegister");
            bLoadOnStart = true;
            return true;
        }

        public bool OnUnregister()
        {
            WriteLifecycle("OnUnregister");
            return true;
        }

        public bool OnInit()
        {
            WriteLifecycle("OnInit");
            return true;
        }

        public bool OnInitGui()
        {
            WriteLifecycle("OnInitGui");
            return true;
        }

        public bool OnExit()
        {
            WriteLifecycle("OnExit");
            return true;
        }

        private static void WriteLifecycle(string eventName)
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsDirectory);
                var line = string.Join(
                    "|",
                    DateTime.UtcNow.ToString("O"),
                    eventName,
                    typeof(EplanAddIn).Assembly.Location);
                File.AppendAllText(LifecycleLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never prevent EPLAN from registering or loading the add-in.
            }
        }
    }
}
