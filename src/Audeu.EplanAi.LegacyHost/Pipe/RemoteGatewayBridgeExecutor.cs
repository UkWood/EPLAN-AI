using System;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Remote;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public sealed class RemoteGatewayBridgeExecutor : IBridgeRequestExecutor
    {
        private readonly RemoteGateway _gateway;
        private readonly TimeSpan _operationTimeout;

        public RemoteGatewayBridgeExecutor(RemoteGateway gateway, TimeSpan operationTimeout)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (operationTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(operationTimeout));
            }

            _operationTimeout = operationTimeout;
        }

        public Task<BridgeResponse> ExecuteAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            return _gateway.ExecuteBridgeRequestAsync(request, _operationTimeout, cancellationToken);
        }
    }
}
