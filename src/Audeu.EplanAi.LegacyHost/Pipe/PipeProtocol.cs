using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
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

            return WriteFrameAsync(
                stream,
                Serialize(PipeBridgeRequest.FromContract(request)),
                MaxRequestBytes,
                cancellationToken);
        }

        public static async Task<BridgeRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] payload = await ReadFrameAsync(stream, MaxRequestBytes, cancellationToken).ConfigureAwait(false);
            return Deserialize<PipeBridgeRequest>(payload).ToContract();
        }

        public static Task WriteResponseAsync(Stream stream, BridgeResponse response, CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            return WriteFrameAsync(
                stream,
                Serialize(PipeBridgeResponse.FromContract(response)),
                MaxResponseBytes,
                cancellationToken);
        }

        public static async Task<BridgeResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] payload = await ReadFrameAsync(stream, MaxResponseBytes, cancellationToken).ConfigureAwait(false);
            return Deserialize<PipeBridgeResponse>(payload).ToContract();
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
                CreateSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] payload)
        {
            using (var stream = new MemoryStream(payload, writable: false))
            {
                return (T)CreateSerializer(typeof(T)).ReadObject(stream);
            }
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(
                type,
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
        }

        [DataContract]
        private sealed class PipeBridgeRequest
        {
            [DataMember(Name = "protocolVersion", Order = 1)]
            public string ProtocolVersion { get; set; }

            [DataMember(Name = "requestId", Order = 2)]
            public Guid RequestId { get; set; }

            [DataMember(Name = "operation", Order = 3)]
            public string Operation { get; set; }

            [DataMember(Name = "mode", Order = 4)]
            public string Mode { get; set; }

            [DataMember(Name = "projectSessionId", Order = 5, EmitDefaultValue = false)]
            public Guid? ProjectSessionId { get; set; }

            [DataMember(Name = "arguments", Order = 6)]
            public Dictionary<string, string> Arguments { get; set; }

            public static PipeBridgeRequest FromContract(BridgeRequest request)
            {
                return new PipeBridgeRequest
                {
                    ProtocolVersion = request.ProtocolVersion,
                    RequestId = request.RequestId,
                    Operation = request.Operation,
                    Mode = request.Mode,
                    ProjectSessionId = request.ProjectSessionId,
                    Arguments = request.Arguments ?? new Dictionary<string, string>()
                };
            }

            public BridgeRequest ToContract()
            {
                return new BridgeRequest
                {
                    ProtocolVersion = ProtocolVersion,
                    RequestId = RequestId,
                    Operation = Operation,
                    Mode = Mode,
                    ProjectSessionId = ProjectSessionId,
                    Arguments = Arguments ?? new Dictionary<string, string>()
                };
            }
        }

        [DataContract]
        private sealed class PipeBridgeResponse
        {
            [DataMember(Name = "protocolVersion", Order = 1)]
            public string ProtocolVersion { get; set; }

            [DataMember(Name = "requestId", Order = 2)]
            public Guid RequestId { get; set; }

            [DataMember(Name = "success", Order = 3)]
            public bool Success { get; set; }

            [DataMember(Name = "errorCode", Order = 4, EmitDefaultValue = false)]
            public string ErrorCode { get; set; }

            [DataMember(Name = "errorMessage", Order = 5, EmitDefaultValue = false)]
            public string ErrorMessage { get; set; }

            [DataMember(Name = "dataJson", Order = 6, EmitDefaultValue = false)]
            public string DataJson { get; set; }

            [DataMember(Name = "warnings", Order = 7)]
            public string[] Warnings { get; set; }

            public static PipeBridgeResponse FromContract(BridgeResponse response)
            {
                return new PipeBridgeResponse
                {
                    ProtocolVersion = response.ProtocolVersion,
                    RequestId = response.RequestId,
                    Success = response.Success,
                    ErrorCode = response.ErrorCode,
                    ErrorMessage = response.ErrorMessage,
                    DataJson = response.DataJson,
                    Warnings = response.Warnings ?? new string[0]
                };
            }

            public BridgeResponse ToContract()
            {
                return new BridgeResponse
                {
                    ProtocolVersion = ProtocolVersion,
                    RequestId = RequestId,
                    Success = Success,
                    ErrorCode = ErrorCode,
                    ErrorMessage = ErrorMessage,
                    DataJson = DataJson,
                    Warnings = Warnings ?? new string[0]
                };
            }
        }
    }
}
