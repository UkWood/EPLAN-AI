using System;
using System.Collections.Generic;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.AddIn.WriteModel;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class WriteOperationServiceTests
    {
        [Fact]
        public void ReadOnlyProjectErrorCode_IsStableProtocolValue()
        {
            Assert.Equal("READ_ONLY_PROJECT", BridgeErrorCodes.ReadOnlyProject);
        }

        [Fact]
        public void Apply_RequiresWriteModeBeforeAdapterMutation()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();
            request.Mode = "read";

            var response = fixture.Service.Apply(request);

            AssertError(response, BridgeErrorCodes.ValidationFailed);
            Assert.Empty(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_RejectsStaleSessionBeforeAdapterMutation()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();
            request.ProjectSessionId = Guid.NewGuid();

            var response = fixture.Service.Apply(request);

            AssertError(response, BridgeErrorCodes.SessionStale);
            Assert.Empty(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_RejectsReadOnlyProjectBeforeAdapterMutation()
        {
            var fixture = Fixture.Create(isReadOnly: true);

            var response = fixture.Service.Apply(fixture.ApplyRequest());

            AssertError(response, BridgeErrorCodes.ReadOnlyProject);
            Assert.Empty(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_RejectsMalformedOrExtraArgumentsBeforeAdapterMutation()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();
            request.Arguments["propertyId"] = "20011";

            var response = fixture.Service.Apply(request);

            AssertError(response, BridgeErrorCodes.ValidationFailed);
            Assert.Empty(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_RejectsTextOver1024BeforeAdapterMutation()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();
            request.Arguments["newValue"] = new string('x', 1025);

            var response = fixture.Service.Apply(request);

            AssertError(response, BridgeErrorCodes.ValidationFailed);
            Assert.Empty(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_PassesExactFixedCommandAndStoresLastChange()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();

            var response = fixture.Service.Apply(request);

            Assert.True(response.Success);
            var command = Assert.Single(fixture.Adapter.Commands);
            Assert.Equal(Fixture.DatabaseIdentifier, command.DatabaseIdentifier);
            Assert.Equal(Fixture.TypeIdentifier, command.TypeIdentifier);
            Assert.Equal(Fixture.ObjectIdentifier, command.ObjectIdentifier);
            Assert.Equal(Fixture.StringIdentifier, command.StringIdentifier);
            Assert.Equal("Old text", command.ExpectedCurrentValue);
            Assert.Equal("New text", command.NewValue);
            Assert.False(command.IsUndo);
        }

        [Fact]
        public void Apply_DuplicatePlanId_ReachesAdapterOnlyOnce()
        {
            var fixture = Fixture.Create();
            var request = fixture.ApplyRequest();

            var first = fixture.Service.Apply(request);
            var second = fixture.Service.Apply(request);

            Assert.True(first.Success);
            AssertError(second, BridgeErrorCodes.PlanAlreadyApplied);
            Assert.Single(fixture.Adapter.Commands);
        }

        [Fact]
        public void Apply_FailedAttemptStillClaimsPlanId()
        {
            var fixture = Fixture.Create();
            fixture.Adapter.NextResult = WriteExecutionResult.Error(
                BridgeErrorCodes.EplanActionFailed,
                "Injected failure.");
            var request = fixture.ApplyRequest();

            var first = fixture.Service.Apply(request);
            fixture.Adapter.NextResult = WriteExecutionResult.Ok();
            var second = fixture.Service.Apply(request);

            AssertError(first, BridgeErrorCodes.EplanActionFailed);
            AssertError(second, BridgeErrorCodes.PlanAlreadyApplied);
            Assert.Single(fixture.Adapter.Commands);
        }

        [Fact]
        public void UndoLast_RequiresWriteModeAndNoCallerArguments()
        {
            var fixture = Fixture.Create();
            Assert.True(fixture.Service.Apply(fixture.ApplyRequest()).Success);
            var undo = fixture.UndoRequest();
            undo.Arguments["newValue"] = "caller-controlled";

            var response = fixture.Service.UndoLast(undo);

            AssertError(response, BridgeErrorCodes.ValidationFailed);
            Assert.Single(fixture.Adapter.Commands);
        }

        [Fact]
        public void UndoLast_UsesStoredReverseValuesAndClearsStateAfterSuccess()
        {
            var fixture = Fixture.Create();
            Assert.True(fixture.Service.Apply(fixture.ApplyRequest()).Success);

            var undo = fixture.Service.UndoLast(fixture.UndoRequest());
            var secondUndo = fixture.Service.UndoLast(fixture.UndoRequest());

            Assert.True(undo.Success);
            Assert.Equal(2, fixture.Adapter.Commands.Count);
            var command = fixture.Adapter.Commands[1];
            Assert.Equal("New text", command.ExpectedCurrentValue);
            Assert.Equal("Old text", command.NewValue);
            Assert.True(command.IsUndo);
            AssertError(secondUndo, BridgeErrorCodes.ValidationFailed);
        }

        [Fact]
        public void UndoLast_FailureRetainsLastChangeForControlledRetry()
        {
            var fixture = Fixture.Create();
            Assert.True(fixture.Service.Apply(fixture.ApplyRequest()).Success);
            fixture.Adapter.NextResult = WriteExecutionResult.Error(
                BridgeErrorCodes.EplanActionFailed,
                "Injected undo failure.");

            var failed = fixture.Service.UndoLast(fixture.UndoRequest());
            fixture.Adapter.NextResult = WriteExecutionResult.Ok();
            var retry = fixture.Service.UndoLast(fixture.UndoRequest());

            AssertError(failed, BridgeErrorCodes.EplanActionFailed);
            Assert.True(retry.Success);
            Assert.Equal(3, fixture.Adapter.Commands.Count);
        }

        [Fact]
        public void UndoLast_RejectsDifferentSessionBeforeReverseMutation()
        {
            var fixture = Fixture.Create();
            Assert.True(fixture.Service.Apply(fixture.ApplyRequest()).Success);
            var undo = fixture.UndoRequest();
            undo.ProjectSessionId = Guid.NewGuid();

            var response = fixture.Service.UndoLast(undo);

            AssertError(response, BridgeErrorCodes.SessionStale);
            Assert.Single(fixture.Adapter.Commands);
        }

        private static void AssertError(BridgeResponse response, string expectedCode)
        {
            Assert.False(response.Success);
            Assert.Equal(expectedCode, response.ErrorCode);
        }

        private sealed class Fixture
        {
            internal const uint DatabaseIdentifier = 25;
            internal const uint TypeIdentifier = 177;
            internal const long ObjectIdentifier = 123456789;
            internal const string StringIdentifier = "===+++УСПД_Э3#-A1";

            private Fixture(
                RecordingWriteAdapter adapter,
                ProjectSessionTracker sessions,
                WriteOperationService service,
                Guid sessionId)
            {
                Adapter = adapter;
                Sessions = sessions;
                Service = service;
                SessionId = sessionId;
            }

            internal RecordingWriteAdapter Adapter { get; }
            internal ProjectSessionTracker Sessions { get; }
            internal WriteOperationService Service { get; }
            internal Guid SessionId { get; }

            internal static Fixture Create(bool isReadOnly = false)
            {
                var adapter = new RecordingWriteAdapter
                {
                    Project = new ProjectSnapshot
                    {
                        DatabaseIdentifier = DatabaseIdentifier,
                        Name = "Test",
                        FullName = "C:\\Test.edb",
                        PageCount = 1,
                        IsReadOnly = isReadOnly
                    }
                };
                var sessions = new ProjectSessionTracker();
                var sessionId = sessions.GetOrCreate(DatabaseIdentifier);
                return new Fixture(adapter, sessions, new WriteOperationService(adapter, sessions), sessionId);
            }

            internal BridgeRequest ApplyRequest()
            {
                return new BridgeRequest
                {
                    ProtocolVersion = ProtocolConstants.Version,
                    RequestId = Guid.NewGuid(),
                    Operation = "change.apply",
                    Mode = "write",
                    ProjectSessionId = SessionId,
                    Arguments = new Dictionary<string, string>
                    {
                        { "changePlanId", Guid.NewGuid().ToString("D") },
                        { "databaseIdentifier", DatabaseIdentifier.ToString() },
                        { "typeIdentifier", TypeIdentifier.ToString() },
                        { "objectIdentifier", ObjectIdentifier.ToString() },
                        { "stringIdentifier", StringIdentifier },
                        { "oldValue", "Old text" },
                        { "newValue", "New text" }
                    }
                };
            }

            internal BridgeRequest UndoRequest()
            {
                return new BridgeRequest
                {
                    ProtocolVersion = ProtocolConstants.Version,
                    RequestId = Guid.NewGuid(),
                    Operation = "change.undoLast",
                    Mode = "write",
                    ProjectSessionId = SessionId,
                    Arguments = new Dictionary<string, string>()
                };
            }
        }

        private sealed class RecordingWriteAdapter : IEplanWriteAdapter
        {
            internal ProjectSnapshot Project { get; set; }
            internal List<FunctionTextWriteCommand> Commands { get; } = new List<FunctionTextWriteCommand>();
            internal WriteExecutionResult NextResult { get; set; } = WriteExecutionResult.Ok();

            public ProjectSnapshot GetActiveProject()
            {
                return Project;
            }

            public WriteExecutionResult ApplyFunctionText(FunctionTextWriteCommand command)
            {
                Commands.Add(command);
                return NextResult;
            }
        }
    }
}
