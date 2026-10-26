using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Spool;

namespace Audeu.EplanAi.LegacyHost.Remote
{
    public sealed class RemoteGateway : IRemoteGatewayLifecycle
    {
        private const string FixedActionName = "AudeuAiDispatch";

        private readonly IRemoteClientAdapter _adapter;
        private readonly SpoolStore _spool;

        public RemoteGateway(IRemoteClientAdapter adapter, SpoolStore spool)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _spool = spool ?? throw new ArgumentNullException(nameof(spool));
        }

        public bool IsConnected { get; private set; }

        public BridgeResponse Connect(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            try
            {
                RemoteServerInfo server = SelectEligibleServer(_adapter.DiscoverLocalServers());
                if (server == null)
                {
                    return Error(Guid.Empty, BridgeErrorCodes.EplanNotFound, "No eligible local EPLAN 2.9 server was found.");
                }

                _adapter.Connect(server, timeout);
                _adapter.SynchronousMode = true;

                if (!_adapter.Ping())
                {
                    PoisonConnection();
                    return Error(Guid.Empty, BridgeErrorCodes.EplanNotConnected, "EPLAN RemoteClient ping failed after connect.");
                }

                IsConnected = true;
                return new BridgeResponse
                {
                    Success = true,
                    DataJson = "{\"connected\":true}"
                };
            }
            catch (Exception exception)
            {
                PoisonConnection();
                return Error(Guid.Empty, BridgeErrorCodes.EplanNotConnected, exception.GetType().Name + ": " + exception.Message);
            }
        }

        public BridgeResponse Ping()
        {
            if (!IsConnected)
            {
                return Error(Guid.Empty, BridgeErrorCodes.EplanNotConnected, "EPLAN RemoteClient is not connected.");
            }

            try
            {
                if (_adapter.Ping())
                {
                    return new BridgeResponse
                    {
                        Success = true,
                        DataJson = "{\"connected\":true}"
                    };
                }
            }
            catch
            {
                // Connection is poisoned below.
            }

            PoisonConnection();
            return Error(Guid.Empty, BridgeErrorCodes.EplanNotConnected, "EPLAN RemoteClient ping failed.");
        }

        public void Disconnect()
        {
            PoisonConnection();
        }

        public async Task<BridgeResponse> ExecuteBridgeRequestAsync(
            BridgeRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsConnected)
            {
                return Error(request.RequestId, BridgeErrorCodes.EplanNotConnected, "EPLAN RemoteClient is not connected.");
            }

            try
            {
                await _spool.WriteRequestAsync(request, cancellationToken).ConfigureAwait(false);

                string action = FixedActionName + " /REQUESTID:" + request.RequestId.ToString("D");
                Task<bool> actionTask = Task.Run(() => _adapter.ExecuteAction(action));
                Task timeoutTask = Task.Delay(timeout, cancellationToken);
                Task completed = await Task.WhenAny(actionTask, timeoutTask).ConfigureAwait(false);

                if (completed != actionTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PoisonConnection();
                    return Error(request.RequestId, BridgeErrorCodes.OperationTimeout, "EPLAN action execution exceeded the operation timeout.");
                }

                bool actionSucceeded;
                try
                {
                    actionSucceeded = await actionTask.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    return Error(request.RequestId, BridgeErrorCodes.EplanActionFailed, exception.GetType().Name + ": " + exception.Message);
                }

                if (!actionSucceeded)
                {
                    return Error(request.RequestId, BridgeErrorCodes.EplanActionFailed, "EPLAN rejected the fixed bridge action.");
                }

                return await _spool.ReadResponseAsync(request.RequestId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Error(request.RequestId, BridgeErrorCodes.InternalError, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void PoisonConnection()
        {
            try
            {
                _adapter.Disconnect();
            }
            catch
            {
                // Disconnect is best effort; the gateway still drops local state.
            }

            IsConnected = false;
        }

        private static RemoteServerInfo SelectEligibleServer(IReadOnlyList<RemoteServerInfo> servers)
        {
            if (servers == null)
            {
                return null;
            }

            return servers
                .Where(server => server != null
                    && server.Is64Bit
                    && !string.IsNullOrWhiteSpace(server.EplanVersion)
                    && server.EplanVersion.StartsWith("2.9", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(server.EplanVariant)
                    && server.EplanVariant.IndexOf("Electric P8", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(server => server.ServerPort)
                .ThenBy(server => server.EplanProcessId)
                .FirstOrDefault();
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
