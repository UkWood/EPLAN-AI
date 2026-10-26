using System;
using System.Collections.Generic;
using System.IO;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Configuration;
using Audeu.EplanAi.LegacyHost.Remote;
using Audeu.EplanAi.LegacyHost.Spool;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class RemoteGatewayRealVariantTests : IDisposable
    {
        private readonly string _root;

        public RemoteGatewayRealVariantTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "Audeu.EplanAi.RemoteGateway.RealVariant", Guid.NewGuid().ToString("N"));
        }

        [Fact]
        public void Connect_AcceptsRealEplan29ProfessionalVariantReportedByWorkstation()
        {
            var adapter = new FakeRemoteClientAdapter();
            adapter.Servers.Add(new RemoteServerInfo
            {
                ServerName = "EplanCommunicationServer",
                ServerPort = 49152,
                EplanProcessId = 6864,
                EplanVariant = "EPLAN Electric P8. Professional",
                EplanVersion = "2.9.4.14773",
                Is64Bit = true
            });

            var gateway = new RemoteGateway(adapter, new SpoolStore(new BridgePaths(_root)));
            var response = gateway.Connect(TimeSpan.FromSeconds(1));

            Assert.True(response.Success);
            Assert.True(adapter.ConnectCalled);
            Assert.Equal(49152, adapter.ConnectedServer.ServerPort);
            Assert.True(adapter.SynchronousMode);
            Assert.True(adapter.PingCalled);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private sealed class FakeRemoteClientAdapter : IRemoteClientAdapter
        {
            public List<RemoteServerInfo> Servers { get; } = new List<RemoteServerInfo>();
            public bool SynchronousMode { get; set; }
            public bool ConnectCalled { get; private set; }
            public RemoteServerInfo ConnectedServer { get; private set; }
            public bool PingCalled { get; private set; }

            public IReadOnlyList<RemoteServerInfo> DiscoverLocalServers() => Servers;

            public void Connect(RemoteServerInfo server, TimeSpan timeout)
            {
                ConnectCalled = true;
                ConnectedServer = server;
            }

            public void Disconnect()
            {
            }

            public bool Ping()
            {
                PingCalled = true;
                return true;
            }

            public bool ExecuteAction(string fullAction)
            {
                return true;
            }
        }
    }
}
