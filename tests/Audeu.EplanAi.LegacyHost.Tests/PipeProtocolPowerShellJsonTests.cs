using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.LegacyHost.Pipe;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class PipeProtocolPowerShellJsonTests
    {
        [Fact]
        public async Task ReadRequest_AcceptsCanonicalCamelCaseJsonUsedByPowerShellClient()
        {
            var requestId = Guid.Parse("bdbc4a96-896c-47a1-ab50-c5a38268e8e9");
            var json = "{\"protocolVersion\":\"1.0\",\"requestId\":\"bdbc4a96-896c-47a1-ab50-c5a38268e8e9\",\"operation\":\"system.status\",\"mode\":\"read\",\"arguments\":{}}";
            var payload = new UTF8Encoding(false).GetBytes(json);
            var frame = new byte[payload.Length + 4];
            frame[0] = (byte)(payload.Length & 0xff);
            frame[1] = (byte)((payload.Length >> 8) & 0xff);
            frame[2] = (byte)((payload.Length >> 16) & 0xff);
            frame[3] = (byte)((payload.Length >> 24) & 0xff);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

            using (var stream = new MemoryStream(frame, writable: false))
            {
                var request = await PipeProtocol.ReadRequestAsync(stream, CancellationToken.None);

                Assert.Equal(requestId, request.RequestId);
                Assert.Equal("1.0", request.ProtocolVersion);
                Assert.Equal("system.status", request.Operation);
                Assert.Equal("read", request.Mode);
                Assert.NotNull(request.Arguments);
                Assert.Empty(request.Arguments);
            }
        }
    }
}
