using System;
using System.Collections.Generic;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.AddIn.WriteModel;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class OperationRegistryWriteTests
    {
        [Fact]
        public void Registry_ExposesOnlyApprovedFixedWriteOperations()
        {
            var tracker = new ProjectSessionTracker();
            var project = Project();
            var registry = new OperationRegistry(
                new ReadOperationService(new FakeReadAdapter(project), tracker),
                new WriteOperationService(new FakeWriteAdapter(project), tracker));
            Func<BridgeRequest, BridgeResponse> handler;

            Assert.True(registry.TryResolve("system.status", out handler));
            Assert.True(registry.TryResolve("project.getActive", out handler));
            Assert.True(registry.TryResolve("project.listPages", out handler));
            Assert.True(registry.TryResolve("selection.get", out handler));
            Assert.True(registry.TryResolve("object.get", out handler));
            Assert.True(registry.TryResolve("change.apply", out handler));
            Assert.True(registry.TryResolve("change.undoLast", out handler));
            Assert.False(registry.TryResolve("change.setProperty", out handler));
            Assert.False(registry.TryResolve("eplan.setProperty", out handler));
        }

        [Fact]
        public void Registry_ReadSessionCanBeUsedByWriteOperationWithSharedTracker()
        {
            var tracker = new ProjectSessionTracker();
            var project = Project();
            var write = new FakeWriteAdapter(project);
            var registry = new OperationRegistry(
                new ReadOperationService(new FakeReadAdapter(project), tracker),
                new WriteOperationService(write, tracker));

            Func<BridgeRequest, BridgeResponse> getActive;
            Assert.True(registry.TryResolve("project.getActive", out getActive));
            BridgeResponse active = getActive(Request("project.getActive", "read"));
            Guid session = ExtractSession(active.DataJson);

            Func<BridgeRequest, BridgeResponse> apply;
            Assert.True(registry.TryResolve("change.apply", out apply));
            BridgeRequest request = Request("change.apply", "write");
            request.ProjectSessionId = session;
            request.Arguments = ApplyArguments();
            BridgeResponse response = apply(request);

            Assert.True(response.Success);
            Assert.Single(write.Commands);
        }

        private static ProjectSnapshot Project()
        {
            return new ProjectSnapshot
            {
                DatabaseIdentifier = 25,
                Name = "Demo",
                FullName = "C:\\Demo.edb",
                PageCount = 1,
                IsReadOnly = false
            };
        }

        private static BridgeRequest Request(string operation, string mode)
        {
            return new BridgeRequest
            {
                ProtocolVersion = ProtocolConstants.Version,
                RequestId = Guid.NewGuid(),
                Operation = operation,
                Mode = mode,
                Arguments = new Dictionary<string, string>()
            };
        }

        private static Dictionary<string, string> ApplyArguments()
        {
            return new Dictionary<string, string>
            {
                { "changePlanId", Guid.NewGuid().ToString("D") },
                { "databaseIdentifier", "25" },
                { "typeIdentifier", "177" },
                { "objectIdentifier", "123456789" },
                { "stringIdentifier", "Function:123456789" },
                { "oldValue", "Old" },
                { "newValue", "New" }
            };
        }

        private static Guid ExtractSession(string json)
        {
            const string marker = "\"projectSessionId\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            int end = json.IndexOf('"', start);
            return Guid.Parse(json.Substring(start, end - start));
        }

        private sealed class FakeReadAdapter : IEplanReadAdapter
        {
            private readonly ProjectSnapshot _project;

            public FakeReadAdapter(ProjectSnapshot project)
            {
                _project = project;
            }

            public ProjectSnapshot GetActiveProject() => _project;
            public IList<PageSnapshot> GetPages(uint databaseIdentifier) => new List<PageSnapshot>();
            public IList<ObjectSnapshot> GetSelection(uint databaseIdentifier) => new List<ObjectSnapshot>();
            public ObjectSnapshot ResolveObject(uint databaseIdentifier, string stringIdentifier) => null;
        }

        private sealed class FakeWriteAdapter : IEplanWriteAdapter
        {
            private readonly ProjectSnapshot _project;

            public FakeWriteAdapter(ProjectSnapshot project)
            {
                _project = project;
            }

            public List<FunctionTextWriteCommand> Commands { get; } = new List<FunctionTextWriteCommand>();

            public ProjectSnapshot GetActiveProject() => _project;

            public WriteExecutionResult ApplyFunctionText(FunctionTextWriteCommand command)
            {
                Commands.Add(command);
                return WriteExecutionResult.Ok();
            }
        }
    }
}
