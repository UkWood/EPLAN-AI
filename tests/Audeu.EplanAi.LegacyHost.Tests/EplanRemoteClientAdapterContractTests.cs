using System;
using Audeu.EplanAi.LegacyHost.Remote;
using Eplan.EplApi.RemoteClient;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class EplanRemoteClientAdapterContractTests : IDisposable
    {
        public EplanRemoteClientAdapterContractTests()
        {
            EplanRemoteClient.ResetTestState();
        }

        [Fact]
        public void DiscoverLocalServers_ProjectsVerifiedEplanServerDataMembers()
        {
            EplanRemoteClient.ActiveServers.Add(new EplanServerData(4321)
            {
                EplanPath = @"C:\Program Files\EPLAN\Platform\2.9.4\Bin\EPLAN.exe",
                EplanVariant = "Electric P8",
                EplanVersion = "2.9.4",
                ErrorMsg = null,
                Is64Bit = true,
                ServerName = "EPLAN_4321",
                ServerPort = 49155
            });
            var adapter = new EplanRemoteClientAdapter();

            var servers = adapter.DiscoverLocalServers();

            Assert.Single(servers);
            Assert.Equal("EPLAN_4321", servers[0].ServerName);
            Assert.Equal(49155, servers[0].ServerPort);
            Assert.Equal(4321, servers[0].EplanProcessId);
            Assert.Equal("Electric P8", servers[0].EplanVariant);
            Assert.Equal("2.9.4", servers[0].EplanVersion);
            Assert.True(servers[0].Is64Bit);
        }

        [Fact]
        public void Connect_UsesLocalhostInvariantPortAndTimeout()
        {
            var adapter = new EplanRemoteClientAdapter();
            var server = new RemoteServerInfo { ServerName = "ignored", ServerPort = 49155 };
            var timeout = TimeSpan.FromSeconds(7);

            adapter.Connect(server, timeout);

            Assert.Equal("localhost", EplanRemoteClient.LastConnectComputer);
            Assert.Equal("49155", EplanRemoteClient.LastConnectPort);
            Assert.Equal(timeout, EplanRemoteClient.LastConnectTimeout);
        }

        [Fact]
        public void SyncPingExecuteDisconnect_DelegateToRemoteClient()
        {
            var adapter = new EplanRemoteClientAdapter();
            EplanRemoteClient.PingResult = true;
            EplanRemoteClient.ExecuteActionResult = true;

            adapter.SynchronousMode = true;
            var ping = adapter.Ping();
            var action = adapter.ExecuteAction("AudeuAiDispatch /REQUESTID:11111111-1111-1111-1111-111111111111");
            adapter.Disconnect();

            Assert.True(adapter.SynchronousMode);
            Assert.True(ping);
            Assert.True(action);
            Assert.Equal("AudeuAiDispatch /REQUESTID:11111111-1111-1111-1111-111111111111", EplanRemoteClient.LastAction);
            Assert.True(EplanRemoteClient.DisconnectCalled);
        }

        public void Dispose()
        {
            EplanRemoteClient.ResetTestState();
        }
    }
}
