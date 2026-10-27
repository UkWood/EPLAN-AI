using System;

namespace Audeu.EplanAi.Contracts
{
    public sealed class ChangeOperation
    {
        private readonly Guid _projectSessionId;
        private readonly uint _databaseIdentifier;
        private readonly uint _typeIdentifier;
        private readonly long _objectIdentifier;
        private readonly string _stringIdentifier;

        public ChangeOperation(string kind, ObjectReference target, string oldValue, string newValue)
        {
            Kind = kind;
            _projectSessionId = target.ProjectSessionId;
            _databaseIdentifier = target.DatabaseIdentifier;
            _typeIdentifier = target.TypeIdentifier;
            _objectIdentifier = target.ObjectIdentifier;
            _stringIdentifier = target.StringIdentifier;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string Kind { get; }

        public ObjectReference Target => new ObjectReference
        {
            ProjectSessionId = _projectSessionId,
            DatabaseIdentifier = _databaseIdentifier,
            TypeIdentifier = _typeIdentifier,
            ObjectIdentifier = _objectIdentifier,
            StringIdentifier = _stringIdentifier
        };

        public string OldValue { get; }

        public string NewValue { get; }
    }
}
