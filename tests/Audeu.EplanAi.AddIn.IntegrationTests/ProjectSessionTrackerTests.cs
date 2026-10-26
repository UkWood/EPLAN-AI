using System;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class ProjectSessionTrackerTests
    {
        [Fact]
        public void SameDatabaseIdentifier_ReusesSession()
        {
            var tracker = new ProjectSessionTracker();

            var first = tracker.GetOrCreate(7);
            var second = tracker.GetOrCreate(7);

            Assert.NotEqual(Guid.Empty, first);
            Assert.Equal(first, second);
            Assert.True(tracker.IsCurrent(first, 7));
        }

        [Fact]
        public void ChangedDatabaseIdentifier_CreatesNewSessionAndStalesOldSession()
        {
            var tracker = new ProjectSessionTracker();
            var oldSession = tracker.GetOrCreate(7);

            var newSession = tracker.GetOrCreate(8);

            Assert.NotEqual(oldSession, newSession);
            Assert.False(tracker.IsCurrent(oldSession, 7));
            Assert.True(tracker.IsCurrent(newSession, 8));
        }

        [Fact]
        public void Clear_InvalidatesCurrentSession()
        {
            var tracker = new ProjectSessionTracker();
            var session = tracker.GetOrCreate(7);

            tracker.Clear();

            Assert.False(tracker.IsCurrent(session, 7));
        }

        [Fact]
        public void ReadOnlyErrorCodes_AreStable()
        {
            Assert.Equal("NO_ACTIVE_PROJECT", BridgeErrorCodes.NoActiveProject);
            Assert.Equal("OBJECT_NOT_FOUND", BridgeErrorCodes.ObjectNotFound);
            Assert.Equal("OBJECT_INVALID", BridgeErrorCodes.ObjectInvalid);
            Assert.Equal("OBJECT_TYPE_NOT_ALLOWED", BridgeErrorCodes.ObjectTypeNotAllowed);
            Assert.Equal("VALIDATION_FAILED", BridgeErrorCodes.ValidationFailed);
        }
    }
}
