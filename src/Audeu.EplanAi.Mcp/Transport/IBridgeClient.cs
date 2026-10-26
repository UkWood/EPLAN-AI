using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.Mcp.Transport;

public interface IBridgeClient
{
    Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken);
}
