using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.Mcp.Transport;

public sealed class NamedPipeBridgeClient : IBridgeClient
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _connectTimeout;

    public NamedPipeBridgeClient()
        : this(ConnectTimeout)
    {
    }

    internal NamedPipeBridgeClient(TimeSpan connectTimeout)
    {
        if (connectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeout));
        }

        _connectTimeout = connectTimeout;
    }

    public async Task<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var pipe = new NamedPipeClientStream(
            ".",
            BridgePipeProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using (var timeout = new CancellationTokenSource(_connectTimeout))
        using (var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                   cancellationToken,
                   timeout.Token))
        {
            try
            {
                await pipe.ConnectAsync(connectCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(
                    request.RequestId,
                    BridgeErrorCodes.OperationTimeout,
                    "Timed out connecting to the EPLAN LegacyHost named pipe.");
            }
            catch (IOException)
            {
                return Failure(
                    request.RequestId,
                    BridgeErrorCodes.EplanNotConnected,
                    "The EPLAN LegacyHost named pipe is unavailable.");
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(
                    request.RequestId,
                    BridgeErrorCodes.EplanNotConnected,
                    "Access to the EPLAN LegacyHost named pipe was denied.");
            }
        }

        try
        {
            await BridgePipeProtocol.WriteRequestAsync(pipe, request, cancellationToken).ConfigureAwait(false);
            BridgeResponse response = await BridgePipeProtocol.ReadResponseAsync(pipe, cancellationToken).ConfigureAwait(false);

            if (response.RequestId != request.RequestId)
            {
                return Failure(
                    request.RequestId,
                    BridgeErrorCodes.InternalError,
                    "LegacyHost returned a response for a different request.");
            }

            if (!string.Equals(
                    response.ProtocolVersion,
                    ProtocolConstants.Version,
                    StringComparison.Ordinal))
            {
                return Failure(
                    request.RequestId,
                    BridgeErrorCodes.ProtocolMismatch,
                    "LegacyHost returned an unsupported protocol version.");
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return Failure(
                request.RequestId,
                BridgeErrorCodes.InternalError,
                "LegacyHost returned an invalid framed response.");
        }
        catch (EndOfStreamException)
        {
            return Failure(
                request.RequestId,
                BridgeErrorCodes.EplanNotConnected,
                "The EPLAN LegacyHost named pipe closed before a complete response was received.");
        }
        catch (IOException)
        {
            return Failure(
                request.RequestId,
                BridgeErrorCodes.EplanNotConnected,
                "Communication with the EPLAN LegacyHost named pipe failed.");
        }
    }

    private static BridgeResponse Failure(Guid requestId, string errorCode, string errorMessage) => new()
    {
        ProtocolVersion = ProtocolConstants.Version,
        RequestId = requestId,
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Warnings = Array.Empty<string>()
    };
}
