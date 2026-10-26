using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public interface IBridgeRequestExecutor
    {
        Task<BridgeResponse> ExecuteAsync(BridgeRequest request, CancellationToken cancellationToken);
    }
}
