using System;

namespace Audeu.EplanAi.Contracts
{
    public sealed class ObjectReference
    {
        public Guid ProjectSessionId { get; set; }

        public uint DatabaseIdentifier { get; set; }

        public uint TypeIdentifier { get; set; }

        public long ObjectIdentifier { get; set; }

        public string StringIdentifier { get; set; }
    }
}
