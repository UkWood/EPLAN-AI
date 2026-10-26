using System;
using System.Collections.Generic;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class ReadOperationServiceTests
    {
        [Fact]
        public void GetActiveProject_NoProject_ReturnsStableError()
        {
            var service = CreateService(new FakeReadAdapter());
            var request = Request("project.getActive");

            var response = service.GetActiveProject(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.NoActiveProject, response.ErrorCode);
        }

        [Fact]
        public void GetActiveProject_ReturnsSessionAndCompactProject()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 2) };
            var service = CreateService(adapter);

            var response = service.GetActiveProject(Request("project.getActive"));

            Assert.True(response.Success);
            Assert.Contains("\"projectSessionId\":", response.DataJson);
            Assert.Contains("\"databaseIdentifier\":7", response.DataJson);
            Assert.Contains("\"name\":\"Demo\"", response.DataJson);
            Assert.Contains("\"pageCount\":2", response.DataJson);
        }

        [Fact]
        public void ListPages_ChangedDatabaseIdentifier_ReturnsSessionStale()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "First", 1) };
            var tracker = new ProjectSessionTracker();
            var service = new ReadOperationService(adapter, tracker);
            var active = service.GetActiveProject(Request("project.getActive"));
            var oldSession = ExtractSession(active.DataJson);
            adapter.Project = Project(8, "Second", 1);

            var request = Request("project.listPages");
            request.ProjectSessionId = oldSession;
            var response = service.ListPages(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.SessionStale, response.ErrorCode);
        }

        [Fact]
        public void ListPages_CurrentSession_ReturnsCompactOrderedPages()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 2) };
            adapter.Pages.Add(new PageSnapshot { Ordinal = 1, Name = "=A/1", PageType = "Schematic" });
            adapter.Pages.Add(new PageSnapshot { Ordinal = 2, Name = "=A/2", PageType = "Schematic" });
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = Request("project.listPages");
            request.ProjectSessionId = session;

            var response = service.ListPages(request);

            Assert.True(response.Success);
            Assert.Contains("\"ordinal\":1", response.DataJson);
            Assert.Contains("\"name\":\"=A\\/1\"", response.DataJson);
            Assert.Contains("\"ordinal\":2", response.DataJson);
        }

        [Fact]
        public void GetSelection_EmptySelection_ReturnsEmptyList()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = Request("selection.get");
            request.ProjectSessionId = session;

            var response = service.GetSelection(request);

            Assert.True(response.Success);
            Assert.Contains("\"items\":[]", response.DataJson);
        }

        [Fact]
        public void GetObject_MalformedReferenceArguments_ReturnsValidationFailed()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = Request("object.get");
            request.ProjectSessionId = session;
            request.Arguments["databaseIdentifier"] = "not-a-number";
            request.Arguments["typeIdentifier"] = "27";
            request.Arguments["objectIdentifier"] = "1482";
            request.Arguments["stringIdentifier"] = "Function:1482";

            var response = service.GetObject(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ValidationFailed, response.ErrorCode);
        }

        [Fact]
        public void GetObject_MissingResolvedObject_ReturnsObjectNotFound()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = ObjectRequest(session, 7, 27, 1482, "missing");

            var response = service.GetObject(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ObjectNotFound, response.ErrorCode);
        }

        [Fact]
        public void GetObject_ResolvedIdentityMismatch_ReturnsObjectInvalid()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            adapter.Resolved = FunctionObject(7, 27, 9999, "Function:9999");
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = ObjectRequest(session, 7, 27, 1482, "Function:1482");

            var response = service.GetObject(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ObjectInvalid, response.ErrorCode);
        }

        [Fact]
        public void GetObject_NonFunction_ReturnsObjectTypeNotAllowed()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            adapter.Resolved = new ObjectSnapshot
            {
                DatabaseIdentifier = 7,
                TypeIdentifier = 10,
                ObjectIdentifier = 99,
                StringIdentifier = "Page:99",
                IsValid = true,
                IsFunction = false,
                Kind = "Page"
            };
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = ObjectRequest(session, 7, 10, 99, "Page:99");

            var response = service.GetObject(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ObjectTypeNotAllowed, response.ErrorCode);
        }

        [Fact]
        public void GetObject_Function_ReturnsWhitelistedFieldsOnly()
        {
            var adapter = new FakeReadAdapter { Project = Project(7, "Demo", 0) };
            adapter.Resolved = FunctionObject(7, 27, 1482, "Function:1482");
            adapter.Resolved.Name = "-K1";
            adapter.Resolved.Page = "=A/1";
            adapter.Resolved.FunctionText = "Pump contactor";
            adapter.Resolved.DeviceTag = "-K1";
            var service = CreateService(adapter);
            var session = ExtractSession(service.GetActiveProject(Request("project.getActive")).DataJson);
            var request = ObjectRequest(session, 7, 27, 1482, "Function:1482");

            var response = service.GetObject(request);

            Assert.True(response.Success);
            Assert.Contains("\"kind\":\"Function\"", response.DataJson);
            Assert.Contains("\"functionText\":\"Pump contactor\"", response.DataJson);
            Assert.Contains("\"deviceTag\":\"-K1\"", response.DataJson);
            Assert.DoesNotContain("Properties", response.DataJson);
        }

        private static ReadOperationService CreateService(FakeReadAdapter adapter)
        {
            return new ReadOperationService(adapter, new ProjectSessionTracker());
        }

        private static BridgeRequest Request(string operation)
        {
            return new BridgeRequest { RequestId = Guid.NewGuid(), Operation = operation, Mode = "read" };
        }

        private static BridgeRequest ObjectRequest(Guid session, uint databaseId, uint typeId, long objectId, string stringId)
        {
            var request = Request("object.get");
            request.ProjectSessionId = session;
            request.Arguments["databaseIdentifier"] = databaseId.ToString();
            request.Arguments["typeIdentifier"] = typeId.ToString();
            request.Arguments["objectIdentifier"] = objectId.ToString();
            request.Arguments["stringIdentifier"] = stringId;
            return request;
        }

        private static ProjectSnapshot Project(uint databaseId, string name, int pages)
        {
            return new ProjectSnapshot
            {
                DatabaseIdentifier = databaseId,
                Name = name,
                FullName = "C:\\Projects\\" + name + ".elk",
                PageCount = pages,
                IsReadOnly = false
            };
        }

        private static ObjectSnapshot FunctionObject(uint databaseId, uint typeId, long objectId, string stringId)
        {
            return new ObjectSnapshot
            {
                DatabaseIdentifier = databaseId,
                TypeIdentifier = typeId,
                ObjectIdentifier = objectId,
                StringIdentifier = stringId,
                IsValid = true,
                IsFunction = true,
                Kind = "Function"
            };
        }

        private static Guid ExtractSession(string json)
        {
            const string marker = "\"projectSessionId\":\"";
            var start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = json.IndexOf('"', start);
            return Guid.Parse(json.Substring(start, end - start));
        }

        private sealed class FakeReadAdapter : IEplanReadAdapter
        {
            public ProjectSnapshot Project { get; set; }
            public List<PageSnapshot> Pages { get; } = new List<PageSnapshot>();
            public List<ObjectSnapshot> Selection { get; } = new List<ObjectSnapshot>();
            public ObjectSnapshot Resolved { get; set; }

            public ProjectSnapshot GetActiveProject() => Project;
            public IList<PageSnapshot> GetPages(uint databaseIdentifier) => Pages;
            public IList<ObjectSnapshot> GetSelection(uint databaseIdentifier) => Selection;
            public ObjectSnapshot ResolveObject(uint databaseIdentifier, string stringIdentifier) => Resolved;
        }
    }
}
