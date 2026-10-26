using System;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Hosting;
using Audeu.EplanAi.LegacyHost.Pipe;
using Audeu.EplanAi.LegacyHost.Remote;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class PersistentLegacyHostTests
    {
        [Fact]
        public async Task RunAsync_ConnectFailureDoesNotStartPipeServer()
        {
            var gateway = new FakeGateway
            {
                ConnectResponse = Failure(BridgeErrorCodes.EplanNotFound)
            };
            var server = new FakePipeServer();
            var owned = new FakeDisposable();
            using (var host = new PersistentLegacyHost(gateway, server, owned, TimeSpan.FromSeconds(7)))
            {
                var result = await host.RunAsync(CancellationToken.None);

                Assert.False(result.Success);
                Assert.Equal(BridgeErrorCodes.EplanNotFound, result.ErrorCode);
                Assert.Equal(TimeSpan.FromSeconds(7), gateway.ConnectTimeout);
                Assert.Equal(0, server.RunCount);
                Assert.Equal(0, gateway.DisconnectCount);
            }

            Assert.Equal(1, server.DisposeCount);
            Assert.Equal(1, owned.DisposeCount);
        }

        [Fact]
        public async Task RunAsync_SuccessRunsPipeAndDisconnectsExactlyOnce()
        {
            var gateway = new FakeGateway();
            var server = new FakePipeServer();
            var owned = new FakeDisposable();
            var host = new PersistentLegacyHost(gateway, server, owned, TimeSpan.FromSeconds(10));

            var result = await host.RunAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, server.RunCount);
            Assert.Equal(1, gateway.DisconnectCount);

            host.Dispose();
            Assert.Equal(1, gateway.DisconnectCount);
            Assert.Equal(1, server.DisposeCount);
            Assert.Equal(1, owned.DisposeCount);
        }

        [Fact]
        public async Task RunAsync_CancellationReachesServerAndStillDisconnects()
        {
            var gateway = new FakeGateway();
            var server = new FakePipeServer { WaitForCancellation = true };
            var owned = new FakeDisposable();
            using (var host = new PersistentLegacyHost(gateway, server, owned, TimeSpan.FromSeconds(10)))
            using (var cancellation = new CancellationTokenSource())
            {
                Task<BridgeResponse> run = host.RunAsync(cancellation.Token);
                await server.Entered.Task;
                cancellation.Cancel();

                var result = await run;

                Assert.True(result.Success);
                Assert.True(server.SawCancellation);
                Assert.Equal(1, gateway.DisconnectCount);
            }
        }

        [Fact]
        public void Constructor_RejectsNonPositiveConnectTimeout()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PersistentLegacyHost(
                    new FakeGateway(),
                    new FakePipeServer(),
                    new FakeDisposable(),
                    TimeSpan.Zero));
        }

        private static BridgeResponse Failure(string code)
        {
            return new BridgeResponse
            {
                Success = false,
                ErrorCode = code,
                ErrorMessage = "failure"
            };
        }

        private sealed class FakeGateway : IRemoteGatewayLifecycle
        {
            public BridgeResponse ConnectResponse { get; set; } = new BridgeResponse { Success = true };
            public TimeSpan ConnectTimeout { get; private set; }
            public int DisconnectCount { get; private set; }

            public BridgeResponse Connect(TimeSpan timeout)
            {
                ConnectTimeout = timeout;
                return ConnectResponse;
            }

            public void Disconnect()
            {
                DisconnectCount++;
            }
        }

        private sealed class FakePipeServer : ILegacyHostPipeServer
        {
            public int RunCount { get; private set; }
            public int DisposeCount { get; private set; }
            public bool WaitForCancellation { get; set; }
            public bool SawCancellation { get; private set; }
            public TaskCompletionSource<bool> Entered { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task RunAsync(CancellationToken cancellationToken)
            {
                RunCount++;
                Entered.TrySetResult(true);
                if (!WaitForCancellation)
                {
                    return;
                }

                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => completion.TrySetResult(true)))
                {
                    await completion.Task.ConfigureAwait(false);
                }
                SawCancellation = cancellationToken.IsCancellationRequested;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class FakeDisposable : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
