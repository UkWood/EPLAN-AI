using System;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.Dispatch
{
    public sealed class RequestDispatcher
    {
        private readonly OperationRegistry _operations;

        public RequestDispatcher()
            : this(new OperationRegistry())
        {
        }

        public RequestDispatcher(OperationRegistry operations)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public BridgeResponse Dispatch(BridgeRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!ProtocolConstants.IsSupported(request.ProtocolVersion))
            {
                return Error(request.RequestId, BridgeErrorCodes.ProtocolMismatch, "Unsupported bridge protocol version.");
            }

            Func<BridgeRequest, BridgeResponse> handler;
            if (!_operations.TryResolve(request.Operation, out handler))
            {
                return Error(request.RequestId, BridgeErrorCodes.BridgeUnknownOperation, "Bridge operation is not registered.");
            }

            return handler(request);
        }

        private static BridgeResponse Error(Guid requestId, string errorCode, string message)
        {
            return new BridgeResponse
            {
                RequestId = requestId,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
        }
    }
}
