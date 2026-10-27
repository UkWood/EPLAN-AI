using System;
using Eplan.EplApi.DataModel;

namespace Audeu.EplanAi.AddIn.WriteModel
{
    public sealed class GateCTestFailureHook : IWriteExecutionHook
    {
        public const string EnvironmentVariableName = "AUDEU_EPLAN_AI_GATE_C_FAIL_NEXT_WRITE";

        private readonly Func<string> _readArmingValue;
        private readonly Action _clearArmingValue;

        public GateCTestFailureHook()
            : this(
                () => Environment.GetEnvironmentVariable(EnvironmentVariableName),
                () => Environment.SetEnvironmentVariable(EnvironmentVariableName, null))
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
    }
}
