using System;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Pipe;
using Audeu.EplanAi.LegacyHost.Remote;

namespace Audeu.EplanAi.LegacyHost.Hosting
{
    public sealed class PersistentLegacyHost : IDisposable
    {
        private readonly IRemoteGatewayLifecycle _gateway;
        private readonly ILegacyHostPipeServer _server;
        private readonly IDisposable _ownedResource;
        private readonly TimeSpan _connectTimeout;
        private int _disconnectState;
        private int _disposeState;
        private bool _connected;

        public PersistentLegacyHost(
            IRemoteGatewayLifecycle gateway,
            ILegacyHostPipeServer server,
            IDisposable ownedResource,
            TimeSpan connectTimeout)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _ownedResource = ownedResource ?? throw new ArgumentNullException(nameof(ownedResource));
            if (connectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            }

            _connectTimeout = connectTimeout;
        }

        public async Task<BridgeResponse> RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BridgeResponse connected = _gateway.Connect(_connectTimeout);
            if (connected == null)
            {
                throw new InvalidOperationException("RemoteGateway returned a null connect response.");
            }

            if (!connected.Success)
            {
                return connected;
            }

            _connected = true;
            try
            {
                await _server.RunAsync(cancellationToken).ConfigureAwait(false);
                return new BridgeResponse
                {
                    Success = true,
                    DataJson = "{\"stopped\":true}"
                };
            }
            finally
            {
                DisconnectOnce();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            DisconnectOnce();
            _server.Dispose();
            _ownedResource.Dispose();
        }

        private void DisconnectOnce()
        {
            if (!_connected)
            {
                return;
            }

            if (Interlocked.Exchange(ref _disconnectState, 1) != 0)
            {
                return;
            }

            _gateway.Disconnect();
            _connected = false;
        }
    }
}
