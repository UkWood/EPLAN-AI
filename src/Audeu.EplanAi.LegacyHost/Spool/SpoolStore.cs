using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Configuration;

namespace Audeu.EplanAi.LegacyHost.Spool
{
    public sealed class SpoolStore
    {
        public const int MaxRequestBytes = 1024 * 1024;
        public const int MaxResponseBytes = 16 * 1024 * 1024;

        private readonly BridgePaths _paths;

        public SpoolStore(BridgePaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public async Task WriteRequestAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] payload = Serialize(request);
            if (payload.LongLength > MaxRequestBytes)
            {
                throw new InvalidDataException("Bridge request exceeds the 1 MiB spool limit.");
            }

            Directory.CreateDirectory(_paths.RequestDirectory);

            string finalPath = _paths.GetRequestPath(request.RequestId);
            string tempPath = finalPath + ".tmp";

            if (File.Exists(finalPath))
            {
                throw new IOException("A request with the same request ID already exists in the spool.");
            }

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            try
            {
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true))
                {
                    await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempPath, finalPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public Task<BridgeResponse> ReadResponseAsync(Guid requestId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string responsePath = _paths.GetResponsePath(requestId);
            var file = new FileInfo(responsePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("Bridge response was not found.", responsePath);
            }

            if (file.Length > MaxResponseBytes)
            {
                throw new InvalidDataException("Bridge response exceeds the 16 MiB spool limit.");
            }

            BridgeResponse response;
            using (var stream = new FileStream(responsePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                response = (BridgeResponse)new DataContractJsonSerializer(typeof(BridgeResponse)).ReadObject(stream);
            }

            if (response == null)
            {
                throw new InvalidDataException("Bridge response JSON did not contain a response object.");
            }

            if (response.RequestId != requestId)
            {
                throw new InvalidDataException("Bridge response request ID does not match the requested ID.");
            }

            return Task.FromResult(response);
        }

        public void CleanupStaleFiles(TimeSpan maxAge)
        {
            if (maxAge < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAge));
            }

            DateTime cutoffUtc = DateTime.UtcNow.Subtract(maxAge);
            CleanupDirectory(_paths.RequestDirectory, cutoffUtc);
            CleanupDirectory(_paths.ResponseDirectory, cutoffUtc);
        }

        private static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static void CleanupDirectory(string directory, DateTime cutoffUtc)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(directory))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoffUtc)
                {
                    File.Delete(path);
                }
            }
        }
    }
}
