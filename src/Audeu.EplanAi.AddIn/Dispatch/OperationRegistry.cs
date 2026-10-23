using System;
using System.Collections.Generic;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.Dispatch
{
    public sealed class OperationRegistry
    {
        private readonly Dictionary<string, Func<BridgeRequest, BridgeResponse>> _operations;

        public OperationRegistry()
        {
            _operations = new Dictionary<string, Func<BridgeRequest, BridgeResponse>>(StringComparer.Ordinal)
            {
                { "system.status", CreateSystemStatusResponse }
            };
        }

        public bool TryResolve(string operation, out Func<BridgeRequest, BridgeResponse> handler)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                handler = null;
                return false;
            }

            return _operations.TryGetValue(operation, out handler);
        }

        private static BridgeResponse CreateSystemStatusResponse(BridgeRequest request)
        {
            return new BridgeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                DataJson = "{\"status\":\"ready\",\"protocolVersion\":\"1.0\",\"addinVersion\":\"0.1.0\"}"
            };
        }
    }
}
