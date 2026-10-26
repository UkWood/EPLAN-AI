using System;

namespace Audeu.EplanAi.Contracts
{
    public sealed class BridgeResponse
    {
        public BridgeResponse()
        {
            ProtocolVersion = ProtocolConstants.Version;
            Warnings = new string[0];
        }

        public string ProtocolVersion { get; set; }

        public Guid RequestId { get; set; }

        public bool Success { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public string DataJson { get; set; }

        public string[] Warnings { get; set; }
    }
}
