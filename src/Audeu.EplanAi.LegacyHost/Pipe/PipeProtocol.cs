using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.LegacyHost.Pipe
{
    public static class PipeProtocol
    {
        public const string PipeName = "Audeu.EplanAi.LegacyHost.v1";
        public const int MaxRequestBytes = 1024 * 1024;
        public const int MaxResponseBytes = 16 * 1024 * 1024;

        public static Task WriteRequestAsync(Stream stream, BridgeRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return WriteFrameAsync(stream, Serialize(request), MaxRequestBytes, cancellationToken);
        }

        public static async Task<BridgeRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] payload = await ReadFrameAsync(stream, MaxRequestBytes, cancellationToken).ConfigureAwait(false);
            return Deserialize<BridgeRequest>(payload);
        }

        public static Task WriteResponseAsync(Stream stream, BridgeResponse response, CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            return WriteFrameAsync(stream, Serialize(response), MaxResponseBytes, cancellationToken);
        }

        public static async Task<BridgeResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] payload = await ReadFrameAsync(stream, MaxResponseBytes, cancellationToken).ConfigureAwait(false);
            return Deserialize<BridgeResponse>(payload);
        }

        private static async Task WriteFrameAsync(
            Stream stream,
            byte[] payload,
            int maxBytes,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (payload.Length <= 0 || payload.Length > maxBytes)
            {
                throw new InvalidDataException("Pipe payload length is outside the allowed range.");
            }

            byte[] prefix = new byte[4];
            int length = payload.Length;
            prefix[0] = (byte)(length & 0xff);
            prefix[1] = (byte)((length >> 8) & 0xff);
            prefix[2] = (byte)((length >> 16) & 0xff);
            prefix[3] = (byte)((length >> 24) & 0xff);

            await stream.WriteAsync(prefix, 0, prefix.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadFrameAsync(
            Stream stream,
            int maxBytes,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            byte[] prefix = new byte[4];
            await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);

            int length = prefix[0]
                | (prefix[1] << 8)
                | (prefix[2] << 16)
                | (prefix[3] << 24);

            if (length <= 0 || length > maxBytes)
            {
                throw new InvalidDataException("Pipe payload length is outside the allowed range.");
            }

            var payload = new byte[length];
            await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            return payload;
        }

        private static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    throw new EndOfStreamException("Pipe message ended before the declared frame length was read.");
                }

                offset += read;
            }
        }

        private static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] payload)
        {
            using (var stream = new MemoryStream(payload, writable: false))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }
    }
}
