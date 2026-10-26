using System;
using System.IO;
using System.Runtime.Serialization.Json;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.Serialization
{
    public sealed class SpoolCodec
    {
        public const int MaxRequestBytes = 1024 * 1024;
        public const int MaxResponseBytes = 16 * 1024 * 1024;

        private readonly string _requestDirectory;
        private readonly string _responseDirectory;

        public SpoolCodec(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("A spool root path is required.", nameof(rootPath));
            }

            var root = Path.GetFullPath(rootPath);
            _requestDirectory = Path.Combine(root, "requests");
            _responseDirectory = Path.Combine(root, "responses");
        }

        public static SpoolCodec CreateDefault()
        {
            return new SpoolCodec(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Audeu",
                "EplanAiBridge"));
        }

        public BridgeRequest ReadRequest(Guid requestId)
        {
            var path = GetRequestPath(requestId);
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                throw new FileNotFoundException("Bridge request was not found.", path);
            }

            if (file.Length > MaxRequestBytes)
            {
                throw new InvalidDataException("Bridge request exceeds the 1 MiB spool limit.");
            }

            BridgeRequest request;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SkipUtf8Bom(stream);
                request = (BridgeRequest)new DataContractJsonSerializer(typeof(BridgeRequest)).ReadObject(stream);
            }

            if (request == null || request.RequestId != requestId)
            {
                throw new InvalidDataException("Bridge request ID does not match its spool filename.");
            }

            return request;
        }

        public void WriteResponse(BridgeResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            byte[] payload;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(BridgeResponse)).WriteObject(stream, response);
                payload = stream.ToArray();
            }

            if (payload.LongLength > MaxResponseBytes)
            {
                throw new InvalidDataException("Bridge response exceeds the 16 MiB spool limit.");
            }

            Directory.CreateDirectory(_responseDirectory);
            var finalPath = GetResponsePath(response.RequestId);
            var tempPath = finalPath + ".tmp";

            if (File.Exists(finalPath))
            {
                throw new IOException("A response with the same request ID already exists in the spool.");
            }

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }

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

        private static void SkipUtf8Bom(Stream stream)
        {
            if (stream == null || !stream.CanSeek || stream.Length < 3)
            {
                return;
            }

            var first = stream.ReadByte();
            var second = stream.ReadByte();
            var third = stream.ReadByte();

            if (first != 0xEF || second != 0xBB || third != 0xBF)
            {
                stream.Position = 0;
            }
        }

        private string GetRequestPath(Guid requestId)
        {
            return Path.Combine(_requestDirectory, requestId.ToString("D") + ".request.json");
        }

        private string GetResponsePath(Guid requestId)
        {
            return Path.Combine(_responseDirectory, requestId.ToString("D") + ".response.json");
        }
    }
}
