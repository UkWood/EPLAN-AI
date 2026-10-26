using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public sealed class LegacyHostPipeServer : IDisposable
    {
        private readonly IBridgeRequestExecutor _executor;
        private readonly SemaphoreSlim _executionGate = new SemaphoreSlim(1, 1);

        public LegacyHostPipeServer(IBridgeRequestExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = await PipeProtocol.ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);

            await _executionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var response = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                if (response == null)
                {
                    throw new InvalidDataException("Bridge request executor returned a null response.");
                }

                await PipeProtocol.WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _executionGate.Release();
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var pipe = CreateServerStream())
                {
                    try
                    {
                        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        await HandleClientAsync(pipe, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (IOException)
                    {
                        // The client disconnected or supplied an invalid/truncated frame.
                    }
                    catch (System.Runtime.Serialization.SerializationException)
                    {
                        // Malformed JSON never reaches the executor; close this client and continue.
                    }
                    catch (InvalidDataException)
                    {
                        // Invalid/oversized framing never reaches the executor; close and continue.
                    }
                }
            }
        }

        public void Dispose()
        {
            _executionGate.Dispose();
        }

        private static NamedPipeServerStream CreateServerStream()
        {
            var sid = CurrentUserPipeSecurity.GetCurrentUserSid();
            var security = CurrentUserPipeSecurity.Create(sid);
            return new NamedPipeServerStream(
                PipeProtocol.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                security);
        }
    }
}
