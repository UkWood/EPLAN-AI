using System;
using System.Collections.Generic;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.AddIn.WriteModel;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.Dispatch
{
    public sealed class OperationRegistry
    {
        private readonly Dictionary<string, Func<BridgeRequest, BridgeResponse>> _operations;

        public OperationRegistry()
        {
            var sessions = new ProjectSessionTracker();
            var readOperations = new ReadOperationService(new EplanReadAdapter(), sessions);
            var writeOperations = new WriteOperationService(new EplanWriteAdapter(), sessions);
            _operations = CreateOperations(readOperations, writeOperations);
        }

        public OperationRegistry(ReadOperationService readOperations)
        {
            _operations = CreateOperations(readOperations, null);
        }

        public OperationRegistry(ReadOperationService readOperations, WriteOperationService writeOperations)
        {
            _operations = CreateOperations(readOperations, writeOperations);
        }

        public bool TryResolve(string operation, out Func<BridgeRequest, BridgeResponse> handler)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                handler = null;
                return false;
            }

            return _operations.TryGetValue(operation, out handler);
        }

        private static Dictionary<string, Func<BridgeRequest, BridgeResponse>> CreateOperations(
            ReadOperationService readOperations,
            WriteOperationService writeOperations)
        {
            if (readOperations == null)
            {
                throw new ArgumentNullException(nameof(readOperations));
            }

            var operations = new Dictionary<string, Func<BridgeRequest, BridgeResponse>>(StringComparer.Ordinal)
            {
                { "system.status", CreateSystemStatusResponse },
                { "project.getActive", readOperations.GetActiveProject },
                { "project.listPages", readOperations.ListPages },
                { "selection.get", readOperations.GetSelection },
                { "object.get", readOperations.GetObject }
            };

            if (writeOperations != null)
            {
                operations.Add("change.apply", writeOperations.Apply);
                operations.Add("change.undoLast", writeOperations.UndoLast);
            }

            return operations;
        }

        private static BridgeResponse CreateSystemStatusResponse(BridgeRequest request)
        {
            return new BridgeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                DataJson = "{\"status\":\"ready\",\"protocolVersion\":\"1.0\",\"addinVersion\":\"0.1.0\"}"
            };
        }
    }
}
