using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Audeu.EplanAi.AddIn.Actions;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.AddIn.Serialization;
using Audeu.EplanAi.Contracts;
using Eplan.EplApi.ApplicationFramework;
using Xunit;

namespace Audeu.EplanAi.AddIn.IntegrationTests
{
    public sealed class DispatchBehaviorTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "EplanAiAddInTests", Guid.NewGuid().ToString("N"));

        [Fact]
        public void Execute_MissingRequestId_FailsClosed()
        {
            var action = CreateAction();
            var context = new ActionCallingContext();

            Assert.False(action.Execute(context));
        }

        [Fact]
        public void Execute_MalformedRequestId_FailsClosed()
        {
            var action = CreateAction();
            var context = new ActionCallingContext();
            context.AddParameter("REQUESTID", "not-a-guid");

            Assert.False(action.Execute(context));
        }

        [Fact]
        public void Execute_UnknownOperation_WritesStableErrorResponse()
        {
            var request = NewRequest("unsafe.arbitrary.action");
            WriteRequest(request);

            var context = ContextFor(request.RequestId);
            Assert.True(CreateAction().Execute(context));

            var response = ReadResponse(request.RequestId);
            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.BridgeUnknownOperation, response.ErrorCode);
            Assert.Equal(request.RequestId, response.RequestId);
        }

        [Fact]
        public void Execute_SystemStatus_RoundTripsThroughSpool()
        {
            var request = NewRequest("system.status");
            WriteRequest(request);

            var context = ContextFor(request.RequestId);
            Assert.True(CreateAction().Execute(context));

            var response = ReadResponse(request.RequestId);
            Assert.True(response.Success);
            Assert.Equal(request.RequestId, response.RequestId);
            Assert.Null(response.ErrorCode);
            Assert.Contains("\"status\":\"ready\"", response.DataJson);
            Assert.Contains("\"protocolVersion\":\"1.0\"", response.DataJson);
        }

        [Fact]
        public void Execute_PowerShellStatusRequestShape_RoundTripsThroughSpool()
        {
            var requestId = Guid.NewGuid();
            var directory = Path.Combine(_root, "requests");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, requestId.ToString("D") + ".request.json");
            var json = "{\"ProtocolVersion\":\"1.0\",\"RequestId\":\"" + requestId.ToString("D") + "\",\"Operation\":\"system.status\",\"Mode\":\"read\",\"ProjectSessionId\":null,\"Arguments\":{}}";
            File.WriteAllText(path, json, new UTF8Encoding(false));

            Assert.True(CreateAction().Execute(ContextFor(requestId)));

            var response = ReadResponse(requestId);
            Assert.True(response.Success);
            Assert.Equal(requestId, response.RequestId);
            Assert.Contains("\"status\":\"ready\"", response.DataJson);
        }

        [Fact]
        public void Execute_PowerShell51Utf8BomStatusRequest_RoundTripsThroughSpool()
        {
            var requestId = Guid.NewGuid();
            var directory = Path.Combine(_root, "requests");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, requestId.ToString("D") + ".request.json");
            var json = "{\"ProtocolVersion\":\"1.0\",\"RequestId\":\"" + requestId.ToString("D") + "\",\"Operation\":\"system.status\",\"Mode\":\"read\",\"ProjectSessionId\":null,\"Arguments\":{}}";
            File.WriteAllText(path, json, new UTF8Encoding(true));

            Assert.True(CreateAction().Execute(ContextFor(requestId)));

            var response = ReadResponse(requestId);
            Assert.True(response.Success);
            Assert.Equal(requestId, response.RequestId);
            Assert.Contains("\"status\":\"ready\"", response.DataJson);
        }

        [Fact]
        public void Execute_ValidRequestIdWhenProcessingThrows_WritesDiagnosticResponse()
        {
            var requestId = Guid.NewGuid();

            Assert.True(CreateAction().Execute(ContextFor(requestId)));

            var response = ReadResponse(requestId);
            Assert.False(response.Success);
            Assert.Equal("INTERNAL_ERROR", response.ErrorCode);
            Assert.Equal(requestId, response.RequestId);
            Assert.False(string.IsNullOrWhiteSpace(response.ErrorMessage));
        }

        [Fact]
        public void Dispatcher_RejectsProtocolMismatchBeforeOperationExecution()
        {
            var request = NewRequest("system.status");
            request.ProtocolVersion = "0.9";

            var response = new RequestDispatcher().Dispatch(request);

            Assert.False(response.Success);
            Assert.Equal(BridgeErrorCodes.ProtocolMismatch, response.ErrorCode);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private AudeuAiDispatchAction CreateAction()
        {
            return new AudeuAiDispatchAction(new SpoolCodec(_root), new RequestDispatcher());
        }

        private static BridgeRequest NewRequest(string operation)
        {
            return new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = operation,
                Mode = "read"
            };
        }

        private ActionCallingContext ContextFor(Guid requestId)
        {
            var context = new ActionCallingContext();
            context.AddParameter("REQUESTID", requestId.ToString("D"));
            return context;
        }

        private void WriteRequest(BridgeRequest request)
        {
            var directory = Path.Combine(_root, "requests");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, request.RequestId.ToString("D") + ".request.json");
            using (var stream = File.Create(path))
            {
                new DataContractJsonSerializer(typeof(BridgeRequest)).WriteObject(stream, request);
            }
        }

        private BridgeResponse ReadResponse(Guid requestId)
        {
            var path = Path.Combine(_root, "responses", requestId.ToString("D") + ".response.json");
            using (var stream = File.OpenRead(path))
            {
                return (BridgeResponse)new DataContractJsonSerializer(typeof(BridgeResponse)).ReadObject(stream);
            }
        }
    }
}
