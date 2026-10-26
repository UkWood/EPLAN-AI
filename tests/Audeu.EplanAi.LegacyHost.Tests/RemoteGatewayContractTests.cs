using System;
using System.Collections.Generic;
using System.Linq;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Remote;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class RemoteGatewayContractTests
    {
        [Fact]
        public void BridgeErrorCodes_ExposeRemoteClientFailures()
        {
            Assert.Equal("EPLAN_NOT_FOUND", BridgeErrorCodes.EplanNotFound);
            Assert.Equal("EPLAN_NOT_CONNECTED", BridgeErrorCodes.EplanNotConnected);
            Assert.Equal("EPLAN_ACTION_FAILED", BridgeErrorCodes.EplanActionFailed);
            Assert.Equal("OPERATION_TIMEOUT", BridgeErrorCodes.OperationTimeout);
        }

        [Fact]
        public void RemoteServerInfo_CarriesOnlyDiscoveryMetadata()
        {
            var server = new RemoteServerInfo
            {
                ServerName = "localhost",
                ServerPort = 49155,
                EplanProcessId = 1234,
                EplanVariant = "Electric P8",
                EplanVersion = "2.9.4",
                Is64Bit = true
            };

            Assert.Equal("localhost", server.ServerName);
            Assert.Equal(49155, server.ServerPort);
            Assert.Equal(1234, server.EplanProcessId);
            Assert.Equal("Electric P8", server.EplanVariant);
            Assert.Equal("2.9.4", server.EplanVersion);
            Assert.True(server.Is64Bit);
        }

        [Fact]
        public void RemoteClientAdapter_SurfaceIsNarrow()
        {
            var methods = typeof(IRemoteClientAdapter)
                .GetMethods()
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.Equal(new[]
            {
                "Connect",
                "Disconnect",
                "DiscoverLocalServers",
                "ExecuteAction",
                "Ping",
                "get_SynchronousMode",
                "set_SynchronousMode"
            }, methods);

            Assert.Equal(typeof(IReadOnlyList<RemoteServerInfo>),
                typeof(IRemoteClientAdapter).GetMethod("DiscoverLocalServers").ReturnType);
            Assert.Equal(typeof(void),
                typeof(IRemoteClientAdapter).GetMethod("Connect").ReturnType);
            Assert.Equal(typeof(bool),
                typeof(IRemoteClientAdapter).GetMethod("Ping").ReturnType);
            Assert.Equal(typeof(bool),
                typeof(IRemoteClientAdapter).GetMethod("ExecuteAction").ReturnType);
        }
    }
}
