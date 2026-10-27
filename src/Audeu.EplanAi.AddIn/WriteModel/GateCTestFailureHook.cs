using System;
using System.IO;
using Eplan.EplApi.DataModel;

namespace Audeu.EplanAi.AddIn.WriteModel
{
    public sealed class GateCTestFailureHook : IWriteExecutionHook
    {
        public const string EnvironmentVariableName = "AUDEU_EPLAN_AI_GATE_C_FAIL_NEXT_WRITE";
        public const string MarkerFileName = "Audeu.EplanAi.GateC.fail-next-write";

        private readonly Func<string> _readArmingValue;
        private readonly Action _clearArmingValue;

        public GateCTestFailureHook()
            : this(ReadDefaultArmingValue, ClearDefaultArmingValue)
        {
        }

        public GateCTestFailureHook(Func<string> readArmingValue, Action clearArmingValue)
        {
            _readArmingValue = readArmingValue ?? throw new ArgumentNullException(nameof(readArmingValue));
            _clearArmingValue = clearArmingValue ?? throw new ArgumentNullException(nameof(clearArmingValue));
        }

        public void AfterAssignment(Function function)
        {
            if (!string.Equals(_readArmingValue(), "1", StringComparison.Ordinal))
            {
                return;
            }

            _clearArmingValue();
            throw new InvalidOperationException(
                "Gate C controlled failure injected after Function Text assignment and before commit.");
        }

        private static string ReadDefaultArmingValue()
        {
            if (string.Equals(
                Environment.GetEnvironmentVariable(EnvironmentVariableName),
                "1",
                StringComparison.Ordinal))
            {
                return "1";
            }

            return File.Exists(GetMarkerFilePath()) ? "1" : null;
        }

        private static void ClearDefaultArmingValue()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            string markerFilePath = GetMarkerFilePath();
            if (File.Exists(markerFilePath))
            {
                File.Delete(markerFilePath);
            }
        }

        private static string GetMarkerFilePath()
        {
            return Path.Combine(Path.GetTempPath(), MarkerFileName);
        }
    }
}
