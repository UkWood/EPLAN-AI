using System;
using Audeu.EplanAi.AddIn.WriteModel;
using Eplan.EplApi.DataModel;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class GateCTestFailureHookTests
    {
        [Fact]
        public void AfterAssignment_NotArmed_DoesNothing()
        {
            bool cleared = false;
            var hook = new GateCTestFailureHook(
                () => null,
                () => cleared = true);

            hook.AfterAssignment(new Function());

            Assert.False(cleared);
        }

        [Fact]
        public void AfterAssignment_Armed_ClearsFirstThenThrowsExactlyOnce()
        {
            string state = "1";
            var hook = new GateCTestFailureHook(
                () => state,
                () => state = null);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => hook.AfterAssignment(new Function()));

            Assert.Contains("Gate C", error.Message, StringComparison.Ordinal);
            Assert.Null(state);

            hook.AfterAssignment(new Function());
        }

        [Fact]
        public void EnvironmentVariableName_IsFixedAndNotCallerSupplied()
        {
            Assert.Equal(
                "AUDEU_EPLAN_AI_GATE_C_FAIL_NEXT_WRITE",
                GateCTestFailureHook.EnvironmentVariableName);
        }

        [Fact]
        public void MarkerFileName_IsFixedAndNotCallerSupplied()
        {
            Assert.Equal(
                "Audeu.EplanAi.GateC.fail-next-write",
                GateCTestFailureHook.MarkerFileName);
        }
    }
}
