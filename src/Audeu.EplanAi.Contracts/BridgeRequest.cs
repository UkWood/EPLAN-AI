using System;
using System.Collections.Generic;

namespace Audeu.EplanAi.Contracts
{
    public sealed class BridgeRequest
    {
        public BridgeRequest()
        {
            ProtocolVersion = ProtocolConstants.Version;
            Arguments = new Dictionary<string, string>();
        }

        public string ProtocolVersion { get; set; }

        public Guid RequestId { get; set; }

        public string Operation { get; set; }

        public string Mode { get; set; }

        public Guid? ProjectSessionId { get; set; }

        public Dictionary<string, string> Arguments { get; set; }
    }
}
