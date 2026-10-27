using System;
using System.Collections.Generic;
using System.Linq;
using Audeu.EplanAi.AddIn.WriteModel;
using Audeu.EplanAi.Contracts;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class EplanWriteAdapterTests
    {
        [Fact]
        public void ApplyFunctionText_NoActiveProject_IsRejectedWithoutMutation()
        {
            ResetStubs();
            SelectionSet.StubCurrentProject = null;
            var adapter = new EplanWriteAdapter();

            WriteExecutionResult result = adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.NoActiveProject);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        [Fact]
        public void ApplyFunctionText_UnresolvedTarget_IsRejectedWithoutMutation()
        {
            var fixture = Fixture.Create();
            StorableObject.StubResolver = _ => null;

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.ObjectNotFound);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        [Fact]
        public void ApplyFunctionText_IdentityMismatch_IsRejectedWithoutMutation()
        {
            var fixture = Fixture.Create();
            fixture.Function.TypeIdentifier = 999;

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.ObjectInvalid);
            Assert.Equal("??_??@Old text;", fixture.Function.Properties.FUNC_TEXT.Value);
        }

        [Fact]
        public void ApplyFunctionText_NonFunctionTarget_IsRejected()
        {
            var fixture = Fixture.Create();
            var page = new Page
            {
                DatabaseIdentifier = Fixture.DatabaseIdentifier,
                TypeIdentifier = Fixture.TypeIdentifier,
                ObjectIdentifier = Fixture.ObjectIdentifier,
                StubStringIdentifier = Fixture.StringIdentifier,
                Project = fixture.Project,
                IsValid = true
            };
            StorableObject.StubResolver = _ => page;

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.ObjectTypeNotAllowed);
        }

        [Fact]
        public void ApplyFunctionText_ReadOnlyProjectOrFunction_IsRejected()
        {
            var projectFixture = Fixture.Create();
            projectFixture.Project.IsReadOnly = true;
            AssertError(
                projectFixture.Adapter.ApplyFunctionText(Command()),
                BridgeErrorCodes.ReadOnlyProject);

            var functionFixture = Fixture.Create();
            functionFixture.Function.IsReadOnly = true;
            AssertError(
                functionFixture.Adapter.ApplyFunctionText(Command()),
                BridgeErrorCodes.ReadOnlyProject);
        }

        [Fact]
        public void ApplyFunctionText_PreviewedOldValueMismatch_IsPlanStaleWithoutMutation()
        {
            var fixture = Fixture.Create();
            fixture.Function.Properties.FUNC_TEXT.Value = "??_??@Human changed it;";

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.PlanStale);
            Assert.Equal("??_??@Human changed it;", fixture.Function.Properties.FUNC_TEXT.Value);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        [Fact]
        public void ApplyFunctionText_SuccessUsesUndoSafetyTransactionAndLanguageIndependentText()
        {
            var fixture = Fixture.Create();

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            Assert.True(result.Success);
            Assert.Equal("??_??@New text;", fixture.Function.Properties.FUNC_TEXT.Value);
            IList<string> events = StubWriteTrace.Events;
            AssertBefore(events, "undo.create", "safety.create");
            AssertBefore(events, "safety.create", "transaction.create");
            AssertBefore(events, "transaction.create", "property.set");
            AssertBefore(events, "property.set", "transaction.commit");
            AssertBefore(events, "transaction.commit", "safety.commit");
            Assert.Contains("undo.dispose", events);
            Assert.DoesNotContain("safety.rollback", events);
        }

        [Fact]
        public void ApplyFunctionText_InjectedFailureAfterAssignment_RollsBackOriginalValue()
        {
            var fixture = Fixture.Create(new ThrowAfterAssignmentHook());

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.EplanActionFailed);
            Assert.Equal("??_??@Old text;", fixture.Function.Properties.FUNC_TEXT.Value);
            Assert.Contains("property.set", StubWriteTrace.Events);
            Assert.Contains("safety.rollback", StubWriteTrace.Events);
            Assert.DoesNotContain("transaction.commit", StubWriteTrace.Events);
            Assert.DoesNotContain("safety.commit", StubWriteTrace.Events);
        }

        [Fact]
        public void ApplyFunctionText_PostConditionMismatch_RollsBackOriginalValue()
        {
            var fixture = Fixture.Create(new ReplaceAfterAssignmentHook("??_??@Unexpected;"));

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.EplanActionFailed);
            Assert.Equal("??_??@Old text;", fixture.Function.Properties.FUNC_TEXT.Value);
            Assert.Contains("safety.rollback", StubWriteTrace.Events);
            Assert.DoesNotContain("transaction.commit", StubWriteTrace.Events);
        }

        [Fact]
        public void ApplyFunctionText_ImplicitEplanTransactionCommit_FailsClosedAndRollsBackStubState()
        {
            var fixture = Fixture.Create();
            StubWriteTrace.TransactionImplicitCommit = true;

            WriteExecutionResult result = fixture.Adapter.ApplyFunctionText(Command());

            AssertError(result, BridgeErrorCodes.EplanActionFailed);
            Assert.Equal("??_??@Old text;", fixture.Function.Properties.FUNC_TEXT.Value);
            Assert.DoesNotContain("transaction.commit", StubWriteTrace.Events);
            Assert.DoesNotContain("safety.commit", StubWriteTrace.Events);
            Assert.Contains("safety.rollback", StubWriteTrace.Events);
        }

        private static FunctionTextWriteCommand Command()
        {
            return new FunctionTextWriteCommand(
                Fixture.DatabaseIdentifier,
                Fixture.TypeIdentifier,
                Fixture.ObjectIdentifier,
                Fixture.StringIdentifier,
                "Old text",
                "New text",
                false);
        }

        private static void AssertError(WriteExecutionResult result, string expectedCode)
        {
            Assert.False(result.Success);
            Assert.Equal(expectedCode, result.ErrorCode);
        }

        private static void AssertBefore(IList<string> events, string first, string second)
        {
            int firstIndex = events.ToList().IndexOf(first);
            int secondIndex = events.ToList().IndexOf(second);
            Assert.True(firstIndex >= 0, $"Missing event '{first}'.");
            Assert.True(secondIndex >= 0, $"Missing event '{second}'.");
            Assert.True(firstIndex < secondIndex, $"Expected '{first}' before '{second}'.");
        }

        private static void ResetStubs()
        {
            StubWriteTrace.Reset();
            SelectionSet.StubCurrentProject = null;
            SelectionSet.StubSelection = new StorableObject[0];
            StorableObject.StubResolver = null;
        }

        private sealed class Fixture
        {
            internal const uint DatabaseIdentifier = 25;
            internal const uint TypeIdentifier = 177;
            internal const long ObjectIdentifier = 123456789;
            internal const string StringIdentifier = "===+++УСПД_Э3#-A1";

            private Fixture(Project project, Function function, EplanWriteAdapter adapter)
            {
                Project = project;
                Function = function;
                Adapter = adapter;
            }

            internal Project Project { get; }
            internal Function Function { get; }
            internal EplanWriteAdapter Adapter { get; }

            internal static Fixture Create(IWriteExecutionHook hook = null)
            {
                ResetStubs();
                var project = new Project
                {
                    DatabaseIdentifier = DatabaseIdentifier,
                    IsReadOnly = false,
                    IsValid = true,
                    ProjectName = "Demo",
                    ProjectFullName = "C:\\Demo.edb"
                };
                var function = new Function
                {
                    DatabaseIdentifier = DatabaseIdentifier,
                    TypeIdentifier = TypeIdentifier,
                    ObjectIdentifier = ObjectIdentifier,
                    StubStringIdentifier = StringIdentifier,
                    Project = project,
                    IsReadOnly = false,
                    IsValid = true,
                    Name = StringIdentifier
                };
                function.Properties.FUNC_TEXT.Value = "??_??@Old text;";
                SelectionSet.StubCurrentProject = project;
                StorableObject.StubResolver = id => id == StringIdentifier ? function : null;
                return new Fixture(
                    project,
                    function,
                    hook == null ? new EplanWriteAdapter() : new EplanWriteAdapter(hook));
            }
        }

        private sealed class ThrowAfterAssignmentHook : IWriteExecutionHook
        {
            public void AfterAssignment(Function function)
            {
                throw new InvalidOperationException("Injected failure after assignment.");
            }
        }

        private sealed class ReplaceAfterAssignmentHook : IWriteExecutionHook
        {
            private readonly string _internalValue;

            internal ReplaceAfterAssignmentHook(string internalValue)
            {
                _internalValue = internalValue;
            }

            public void AfterAssignment(Function function)
            {
                function.Properties.FUNC_TEXT.Value = _internalValue;
            }
        }
    }
}
