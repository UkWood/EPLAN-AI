using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eplan.EplApi.RemoteClient;

namespace Audeu.EplanAi.LegacyHost.Remote
{
    public sealed class EplanRemoteClientAdapter : IRemoteClientAdapter, IDisposable
    {
        private readonly EplanRemoteClient _client;

        public EplanRemoteClientAdapter()
            : this(new EplanRemoteClient())
        {
        }

        internal EplanRemoteClientAdapter(EplanRemoteClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool SynchronousMode
        {
            get { return _client.SynchronousMode; }
            set { _client.SynchronousMode = value; }
        }

        public IReadOnlyList<RemoteServerInfo> DiscoverLocalServers()
        {
            List<EplanServerData> servers;
            _client.GetActiveEplanServersOnLocalMachine(out servers);

            if (servers == null)
            {
                return new RemoteServerInfo[0];
            }

            return servers
                .Where(server => server != null)
                .Select(server => new RemoteServerInfo
                {
                    ServerName = server.ServerName,
                    ServerPort = server.ServerPort,
                    EplanProcessId = server.EplanProcessID,
                    EplanVariant = server.EplanVariant,
                    EplanVersion = server.EplanVersion,
                    Is64Bit = server.Is64Bit
                })
                .ToArray();
        }

        public void Connect(RemoteServerInfo server, TimeSpan timeout)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            _client.Connect(
                "localhost",
                server.ServerPort.ToString(CultureInfo.InvariantCulture),
                timeout);
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }

        public bool Ping()
        {
            return _client.Ping();
        }

        public bool ExecuteAction(string fullAction)
        {
            return _client.ExecuteAction(fullAction);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
