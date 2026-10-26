using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Configuration;
using Audeu.EplanAi.LegacyHost.Remote;
using Audeu.EplanAi.LegacyHost.Spool;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class RemoteGatewayTests : IDisposable
    {
        private readonly string _root;
        private readonly BridgePaths _paths;
        private readonly SpoolStore _spool;

        public RemoteGatewayTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "Audeu.EplanAi.RemoteGateway", Guid.NewGuid().ToString("N"));
            _paths = new BridgePaths(_root);
            _spool = new SpoolStore(_paths);
        }

        [Fact]
        public void Connect_NoEligibleEplan29Server_ReturnsEplanNotFound()
        {
            var adapter = new FakeRemoteClientAdapter();
            adapter.Servers.Add(Server(49160, 5, "2026", true));
            var gateway = new RemoteGateway(adapter, _spool);

            var response = gateway.Connect(TimeSpan.FromSeconds(1));

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.EplanNotFound, response.ErrorCode);
            Assert.False(adapter.ConnectCalled);
        }

        [Fact]
        public void Connect_SelectsLowestPortEligibleEplan29AndEnablesSynchronousMode()
        {
            var adapter = new FakeRemoteClientAdapter();
            adapter.Servers.Add(Server(49160, 20, "2.9.4", true));
            adapter.Servers.Add(Server(49155, 30, "2.9.4", true));
            adapter.Servers.Add(Server(49155, 10, "2.9.4", true));
            adapter.Servers.Add(Server(49154, 1, "2.9.4", false));
            var gateway = new RemoteGateway(adapter, _spool);

            var response = gateway.Connect(TimeSpan.FromSeconds(3));

            Assert.True(response.Success);
            Assert.True(adapter.ConnectCalled);
            Assert.Equal(49155, adapter.ConnectedServer.ServerPort);
            Assert.Equal(10, adapter.ConnectedServer.EplanProcessId);
            Assert.Equal(TimeSpan.FromSeconds(3), adapter.ConnectTimeout);
            Assert.True(adapter.SynchronousMode);
            Assert.True(adapter.PingCalled);
            Assert.True(gateway.IsConnected);
        }

        [Fact]
        public void Connect_PingFails_ReturnsNotConnectedAndDisconnects()
        {
            var adapter = new FakeRemoteClientAdapter { PingResult = false };
            adapter.Servers.Add(Server(49155, 10, "2.9.4", true));
            var gateway = new RemoteGateway(adapter, _spool);

            var response = gateway.Connect(TimeSpan.FromSeconds(1));

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.EplanNotConnected, response.ErrorCode);
            Assert.True(adapter.DisconnectCalled);
            Assert.False(gateway.IsConnected);
        }

        [Fact]
        public async Task ExecuteBridgeRequestAsync_NotConnected_ReturnsStableError()
        {
            var gateway = new RemoteGateway(new FakeRemoteClientAdapter(), _spool);
            var request = Request("system.status");

            var response = await gateway.ExecuteBridgeRequestAsync(request, TimeSpan.FromSeconds(1), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(request.RequestId, response.RequestId);
            Assert.Equal(BridgeErrorCodes.EplanNotConnected, response.ErrorCode);
        }

        [Fact]
        public async Task ExecuteBridgeRequestAsync_UsesOnlyFixedActionAndCanonicalRequestId()
        {
            var adapter = ConnectedAdapter();
            var gateway = new RemoteGateway(adapter, _spool);
            Assert.True(gateway.Connect(TimeSpan.FromSeconds(1)).Success);
            var request = Request("project.getActive");
            adapter.ExecuteHandler = action =>
            {
                WriteResponse(request.RequestId, true, "{\"ok\":true}");
                return true;
            };

            var response = await gateway.ExecuteBridgeRequestAsync(request, TimeSpan.FromSeconds(2), CancellationToken.None);

            Assert.True(response.Success);
            Assert.Equal("AudeuAiDispatch /REQUESTID:" + request.RequestId.ToString("D"), adapter.LastAction);
            Assert.DoesNotContain(request.Operation, adapter.LastAction);
            Assert.True(File.Exists(_paths.GetRequestPath(request.RequestId)));
        }

        [Fact]
        public async Task ExecuteBridgeRequestAsync_ActionReturnsFalse_MapsStableFailure()
        {
            var adapter = ConnectedAdapter();
            adapter.ExecuteResult = false;
            var gateway = new RemoteGateway(adapter, _spool);
            Assert.True(gateway.Connect(TimeSpan.FromSeconds(1)).Success);
            var request = Request("system.status");

            var response = await gateway.ExecuteBridgeRequestAsync(request, TimeSpan.FromSeconds(1), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.EplanActionFailed, response.ErrorCode);
        }

        [Fact]
        public async Task ExecuteBridgeRequestAsync_Timeout_PoisonsConnectionAndDisconnects()
        {
            var adapter = ConnectedAdapter();
            adapter.ExecuteHandler = action =>
            {
                Thread.Sleep(500);
                return true;
            };
            var gateway = new RemoteGateway(adapter, _spool);
            Assert.True(gateway.Connect(TimeSpan.FromSeconds(1)).Success);
            var request = Request("system.status");

            var response = await gateway.ExecuteBridgeRequestAsync(request, TimeSpan.FromMilliseconds(30), CancellationToken.None);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.OperationTimeout, response.ErrorCode);
            Assert.True(adapter.DisconnectCalled);
            Assert.False(gateway.IsConnected);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private FakeRemoteClientAdapter ConnectedAdapter()
        {
            var adapter = new FakeRemoteClientAdapter();
            adapter.Servers.Add(Server(49155, 10, "2.9.4", true));
            return adapter;
        }

        private static RemoteServerInfo Server(int port, int processId, string version, bool is64Bit)
        {
            return new RemoteServerInfo
            {
                ServerName = "localhost",
                ServerPort = port,
                EplanProcessId = processId,
                EplanVariant = "Electric P8",
                EplanVersion = version,
                Is64Bit = is64Bit
            };
        }

        private static BridgeRequest Request(string operation)
        {
            return new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = operation,
                Mode = "read"
            };
        }

        private void WriteResponse(Guid requestId, bool success, string dataJson)
        {
            Directory.CreateDirectory(_paths.ResponseDirectory);
            using (var stream = File.Create(_paths.GetResponsePath(requestId)))
            {
                new DataContractJsonSerializer(typeof(BridgeResponse)).WriteObject(stream, new BridgeResponse
                {
                    RequestId = requestId,
                    Success = success,
                    DataJson = dataJson
                });
            }
        }

        private sealed class FakeRemoteClientAdapter : IRemoteClientAdapter
        {
            public List<RemoteServerInfo> Servers { get; } = new List<RemoteServerInfo>();
            public bool SynchronousMode { get; set; }
            public bool ConnectCalled { get; private set; }
            public RemoteServerInfo ConnectedServer { get; private set; }
            public TimeSpan ConnectTimeout { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public bool PingCalled { get; private set; }
            public bool PingResult { get; set; } = true;
            public bool ExecuteResult { get; set; } = true;
            public Func<string, bool> ExecuteHandler { get; set; }
            public string LastAction { get; private set; }

            public IReadOnlyList<RemoteServerInfo> DiscoverLocalServers() => Servers;

            public void Connect(RemoteServerInfo server, TimeSpan timeout)
            {
                ConnectCalled = true;
                ConnectedServer = server;
                ConnectTimeout = timeout;
            }

            public void Disconnect()
            {
                DisconnectCalled = true;
            }

            public bool Ping()
            {
                PingCalled = true;
                return PingResult;
            }

            public bool ExecuteAction(string fullAction)
            {
                LastAction = fullAction;
                return ExecuteHandler != null ? ExecuteHandler(fullAction) : ExecuteResult;
            }
        }
    }
}
