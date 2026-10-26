using System;

namespace Audeu.EplanAi.AddIn.ReadModel
{
    public sealed class ProjectSessionTracker
    {
        private uint? _databaseIdentifier;
        private Guid _sessionId;

        public Guid GetOrCreate(uint databaseIdentifier)
        {
            if (!_databaseIdentifier.HasValue || _databaseIdentifier.Value != databaseIdentifier || _sessionId == Guid.Empty)
            {
                _databaseIdentifier = databaseIdentifier;
                _sessionId = Guid.NewGuid();
            }

            return _sessionId;
        }

        public bool IsCurrent(Guid sessionId, uint databaseIdentifier)
        {
            return _databaseIdentifier.HasValue
                && _databaseIdentifier.Value == databaseIdentifier
                && _sessionId != Guid.Empty
                && _sessionId == sessionId;
        }

        public void Clear()
        {
            _databaseIdentifier = null;
            _sessionId = Guid.Empty;
        }
    }
}
