using System;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.Contracts;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class DefaultWriteDispatchTests : IDisposable
    {
        public DefaultWriteDispatchTests()
        {
            StubWriteTrace.Reset();
            SelectionSet.StubCurrentProject = new Project
            {
                DatabaseIdentifier = 25,
                IsReadOnly = false,
                IsValid = true,
                ProjectName = "Demo",
                ProjectFullName = "C:\\Demo.edb"
            };
            SelectionSet.StubSelection = new StorableObject[0];
            StorableObject.StubResolver = null;
        }

        [Fact]
        public void DefaultDispatcher_ChangeApply_ReachesFixedWritePolicyWithoutMutation()
        {
            var dispatcher = new RequestDispatcher();
            Guid sessionId = GetCurrentSession(dispatcher);
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "change.apply",
                Mode = "write",
                ProjectSessionId = sessionId
            };

            BridgeResponse response = dispatcher.Dispatch(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ValidationFailed, response.ErrorCode);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        [Fact]
        public void DefaultDispatcher_ChangeUndoLast_ReachesFixedWritePolicyWithoutMutation()
        {
            var dispatcher = new RequestDispatcher();
            Guid sessionId = GetCurrentSession(dispatcher);
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "change.undoLast",
                Mode = "write",
                ProjectSessionId = sessionId
            };

            BridgeResponse response = dispatcher.Dispatch(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ValidationFailed, response.ErrorCode);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        [Fact]
        public void DefaultDispatcher_ArbitraryWriteOperation_RemainsRejected()
        {
            var dispatcher = new RequestDispatcher();
            BridgeResponse response = dispatcher.Dispatch(new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "eplan.setProperty",
                Mode = "write"
            });

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.BridgeUnknownOperation, response.ErrorCode);
            Assert.DoesNotContain("property.set", StubWriteTrace.Events);
        }

        public void Dispose()
        {
            StubWriteTrace.Reset();
            SelectionSet.StubCurrentProject = null;
            SelectionSet.StubSelection = new StorableObject[0];
            StorableObject.StubResolver = null;
        }

        private static Guid GetCurrentSession(RequestDispatcher dispatcher)
        {
            BridgeResponse response = dispatcher.Dispatch(new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "project.getActive",
                Mode = "read"
            });

            Assert.True(response.Success);
            const string marker = "\"projectSessionId\":\"";
            int start = response.DataJson.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0);
            start += marker.Length;
            int end = response.DataJson.IndexOf('"', start);
            Assert.True(end > start);
            return Guid.Parse(response.DataJson.Substring(start, end - start));
        }
    }
}
