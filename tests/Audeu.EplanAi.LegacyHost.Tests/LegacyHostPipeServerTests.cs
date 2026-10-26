using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Pipe;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class LegacyHostPipeServerTests
    {
        [Fact]
        public async Task HandleClient_ExecutesOneRequestAndWritesMatchingResponse()
        {
            var executor = new RecordingExecutor();
            var server = new LegacyHostPipeServer(executor);
            var request = Request("system.status");
            using (var stream = await DuplexMemoryStream.CreateAsync(request))
            {
                await server.HandleClientAsync(stream, CancellationToken.None);

                Assert.Single(executor.Requests);
                Assert.Equal(request.RequestId, executor.Requests[0].RequestId);

                var response = await stream.ReadWrittenResponseAsync();
                Assert.True(response.Success);
                Assert.Equal(request.RequestId, response.RequestId);
                Assert.Equal("{\"operation\":\"system.status\"}", response.DataJson);
            }
        }

        [Fact]
        public async Task HandleClient_OversizedFrameNeverReachesExecutor()
        {
            var executor = new RecordingExecutor();
            var server = new LegacyHostPipeServer(executor);
            int length = PipeProtocol.MaxRequestBytes + 1;
            var prefix = new byte[]
            {
                (byte)(length & 0xff),
                (byte)((length >> 8) & 0xff),
                (byte)((length >> 16) & 0xff),
                (byte)((length >> 24) & 0xff)
            };

            using (var stream = new DuplexMemoryStream(prefix))
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => server.HandleClientAsync(stream, CancellationToken.None));
            }

            Assert.Empty(executor.Requests);
        }

        [Fact]
        public async Task HandleClient_MalformedJsonNeverReachesExecutor()
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("{not-json}");
            byte[] frame = new byte[4 + payload.Length];
            frame[0] = (byte)(payload.Length & 0xff);
            frame[1] = (byte)((payload.Length >> 8) & 0xff);
            frame[2] = (byte)((payload.Length >> 16) & 0xff);
            frame[3] = (byte)((payload.Length >> 24) & 0xff);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

            var executor = new RecordingExecutor();
            var server = new LegacyHostPipeServer(executor);
            using (var stream = new DuplexMemoryStream(frame))
            {
                await Assert.ThrowsAnyAsync<Exception>(
                    () => server.HandleClientAsync(stream, CancellationToken.None));
            }

            Assert.Empty(executor.Requests);
        }

        [Fact]
        public async Task HandleClient_ConcurrentCallsSerializeExecutorWork()
        {
            var executor = new BlockingExecutor();
            var server = new LegacyHostPipeServer(executor);
            using (var first = await DuplexMemoryStream.CreateAsync(Request("first")))
            using (var second = await DuplexMemoryStream.CreateAsync(Request("second")))
            {
                Task firstTask = server.HandleClientAsync(first, CancellationToken.None);
                await executor.FirstEntered.Task;

                Task secondTask = server.HandleClientAsync(second, CancellationToken.None);
                await Task.Delay(100, TestContext.Current.CancellationToken);

                Assert.Equal(1, executor.StartedCount);
                Assert.Equal(1, executor.MaxConcurrent);

                executor.ReleaseFirst.SetResult(true);
                await firstTask;
                await executor.SecondEntered.Task;
                await secondTask;

                Assert.Equal(2, executor.StartedCount);
                Assert.Equal(1, executor.MaxConcurrent);
            }
        }

        [Fact]
        public async Task HandleClient_CancellationBeforeExecutorDoesNotExecute()
        {
            var executor = new RecordingExecutor();
            var server = new LegacyHostPipeServer(executor);
            using (var stream = await DuplexMemoryStream.CreateAsync(Request("system.status")))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => server.HandleClientAsync(stream, cancellation.Token));
            }

            Assert.Empty(executor.Requests);
        }

        [Fact]
        public async Task RunAsync_CurrentUserNamedPipeRoundTripAndCancellationStopsLoop()
        {
            var executor = new RecordingExecutor();
            using (var server = new LegacyHostPipeServer(executor))
            using (var cancellation = new CancellationTokenSource())
            {
                Task serverTask = server.RunAsync(cancellation.Token);
                var request = Request("system.status");

                using (var client = new NamedPipeClientStream(
                    ".",
                    PipeProtocol.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous))
                {
                    client.Connect(5000);
                    await PipeProtocol.WriteRequestAsync(
                        client,
                        request,
                        TestContext.Current.CancellationToken);
                    var response = await PipeProtocol.ReadResponseAsync(
                        client,
                        TestContext.Current.CancellationToken);

                    Assert.True(response.Success);
                    Assert.Equal(request.RequestId, response.RequestId);
                }

                cancellation.Cancel();
                Task completed = await Task.WhenAny(
                    serverTask,
                    Task.Delay(5000, TestContext.Current.CancellationToken));
                Assert.Same(serverTask, completed);
                await serverTask;
                Assert.Single(executor.Requests);
            }
        }

        private static BridgeRequest Request(string operation)
        {
            return new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = operation,
                Mode = "read",
                Arguments = new Dictionary<string, string>()
            };
        }

        private sealed class RecordingExecutor : IBridgeRequestExecutor
        {
            public List<BridgeRequest> Requests { get; } = new List<BridgeRequest>();

            public Task<BridgeResponse> ExecuteAsync(BridgeRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new BridgeResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    DataJson = "{\"operation\":\"" + request.Operation + "\"}"
                });
            }
        }

        private sealed class BlockingExecutor : IBridgeRequestExecutor
        {
            private int _active;
            public int StartedCount { get; private set; }
            public int MaxConcurrent { get; private set; }
            public TaskCompletionSource<bool> FirstEntered { get; } = NewTcs();
            public TaskCompletionSource<bool> SecondEntered { get; } = NewTcs();
            public TaskCompletionSource<bool> ReleaseFirst { get; } = NewTcs();

            public async Task<BridgeResponse> ExecuteAsync(BridgeRequest request, CancellationToken cancellationToken)
            {
                int active = Interlocked.Increment(ref _active);
                StartedCount++;
                if (active > MaxConcurrent)
                {
                    MaxConcurrent = active;
                }

                if (StartedCount == 1)
                {
                    FirstEntered.TrySetResult(true);
                    await ReleaseFirst.Task.ConfigureAwait(false);
                }
                else
                {
                    SecondEntered.TrySetResult(true);
                }

                Interlocked.Decrement(ref _active);
                return new BridgeResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    DataJson = "{}"
                };
            }

            private static TaskCompletionSource<bool> NewTcs()
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private sealed class DuplexMemoryStream : Stream
        {
            private readonly MemoryStream _read;
            private readonly MemoryStream _written = new MemoryStream();

            public DuplexMemoryStream(byte[] readBytes)
            {
                _read = new MemoryStream(readBytes, writable: false);
            }

            public static async Task<DuplexMemoryStream> CreateAsync(BridgeRequest request)
            {
                byte[] bytes;
                using (var buffer = new MemoryStream())
                {
                    await PipeProtocol.WriteRequestAsync(buffer, request, CancellationToken.None);
                    bytes = buffer.ToArray();
                }

                return new DuplexMemoryStream(bytes);
            }

            public async Task<BridgeResponse> ReadWrittenResponseAsync()
            {
                _written.Position = 0;
                return await PipeProtocol.ReadResponseAsync(_written, CancellationToken.None);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _written.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => _written.FlushAsync(cancellationToken);
            public override int Read(byte[] buffer, int offset, int count) => _read.Read(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.ReadAsync(buffer, offset, count, cancellationToken);
            public override void Write(byte[] buffer, int offset, int count) => _written.Write(buffer, offset, count);
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _written.WriteAsync(buffer, offset, count, cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _read.Dispose();
                    _written.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
