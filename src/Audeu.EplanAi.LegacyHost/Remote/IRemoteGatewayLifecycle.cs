using System;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.LegacyHost.Remote
{
    public interface IRemoteGatewayLifecycle
    {
        BridgeResponse Connect(TimeSpan timeout);
        void Disconnect();
    }
}
