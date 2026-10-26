using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Pipe;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class PipeProtocolTests
    {
        [Fact]
        public void Constants_AreFixedAndBounded()
        {
            Assert.Equal("Audeu.EplanAi.LegacyHost.v1", PipeProtocol.PipeName);
            Assert.Equal(1024 * 1024, PipeProtocol.MaxRequestBytes);
            Assert.Equal(16 * 1024 * 1024, PipeProtocol.MaxResponseBytes);
        }

        [Fact]
        public async Task Request_RoundTripsThroughLengthPrefixedUtf8Json()
        {
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "project.getActive",
                Mode = "read",
                Arguments = new Dictionary<string, string> { ["key"] = "значение" }
            };

            using (var stream = new MemoryStream())
            {
                await PipeProtocol.WriteRequestAsync(stream, request, CancellationToken.None);
                stream.Position = 0;

                var actual = await PipeProtocol.ReadRequestAsync(stream, CancellationToken.None);

                Assert.Equal(request.RequestId, actual.RequestId);
                Assert.Equal("project.getActive", actual.Operation);
                Assert.Equal("read", actual.Mode);
                Assert.Equal("значение", actual.Arguments["key"]);
            }
        }

        [Fact]
        public async Task Response_RoundTripsThroughLengthPrefixedUtf8Json()
        {
            var response = new BridgeResponse
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                DataJson = "{\"status\":\"ready\"}"
            };

            using (var stream = new MemoryStream())
            {
                await PipeProtocol.WriteResponseAsync(stream, response, CancellationToken.None);
                stream.Position = 0;

                var actual = await PipeProtocol.ReadResponseAsync(stream, CancellationToken.None);

                Assert.Equal(response.RequestId, actual.RequestId);
                Assert.True(actual.Success);
                Assert.Equal(response.DataJson, actual.DataJson);
            }
        }

        [Fact]
        public async Task ReadRequest_ReassemblesPartialReads()
        {
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "system.status",
                Mode = "read"
            };

            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                await PipeProtocol.WriteRequestAsync(buffer, request, CancellationToken.None);
                bytes = buffer.ToArray();
            }

            using (var stream = new ChunkedReadStream(bytes, 2))
            {
                var actual = await PipeProtocol.ReadRequestAsync(stream, CancellationToken.None);
                Assert.Equal(request.RequestId, actual.RequestId);
                Assert.Equal("system.status", actual.Operation);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1048577)]
        [InlineData(2147483647)]
        public async Task ReadRequest_RejectsInvalidLengthBeforePayloadRead(int length)
        {
            var prefix = new byte[4];
            prefix[0] = (byte)(length & 0xff);
            prefix[1] = (byte)((length >> 8) & 0xff);
            prefix[2] = (byte)((length >> 16) & 0xff);
            prefix[3] = (byte)((length >> 24) & 0xff);

            using (var stream = new MemoryStream(prefix))
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => PipeProtocol.ReadRequestAsync(stream, CancellationToken.None));
            }
        }

        [Fact]
        public async Task ReadRequest_RejectsTruncatedPayload()
        {
            var frame = new byte[] { 5, 0, 0, 0, (byte)'{', (byte)'}' };
            using (var stream = new MemoryStream(frame))
            {
                await Assert.ThrowsAsync<EndOfStreamException>(
                    () => PipeProtocol.ReadRequestAsync(stream, CancellationToken.None));
            }
        }

        private sealed class ChunkedReadStream : Stream
        {
            private readonly MemoryStream _inner;
            private readonly int _maxChunk;

            public ChunkedReadStream(byte[] bytes, int maxChunk)
            {
                _inner = new MemoryStream(bytes);
                _maxChunk = maxChunk;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, Math.Min(count, _maxChunk));
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _inner.ReadAsync(buffer, offset, Math.Min(count, _maxChunk), cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
