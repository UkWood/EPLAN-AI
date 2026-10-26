using System;
using System.Threading;
using System.Threading.Tasks;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public interface ILegacyHostPipeServer : IDisposable
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
