using System;
using System.Collections.Generic;

namespace Audeu.EplanAi.LegacyHost.Remote
{
    public interface IRemoteClientAdapter
    {
        bool SynchronousMode { get; set; }

        IReadOnlyList<RemoteServerInfo> DiscoverLocalServers();

        void Connect(RemoteServerInfo server, TimeSpan timeout);

        void Disconnect();

        bool Ping();

        bool ExecuteAction(string fullAction);
    }
}
