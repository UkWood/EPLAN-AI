using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Configuration;
using Audeu.EplanAi.LegacyHost.Remote;
using Audeu.EplanAi.LegacyHost.Spool;

namespace Audeu.EplanAi.LegacyHost.Diagnostics
{
    internal static class Task6Probe
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

        public static async Task<int> RunAsync()
        {
            using (var adapter = new EplanRemoteClientAdapter())
            {
                var gateway = new RemoteGateway(adapter, new SpoolStore(BridgePaths.CreateDefault()));
                try
                {
                    var connected = gateway.Connect(ConnectTimeout);
                    if (!connected.Success)
                    {
                        PrintFailure("connect", connected);
                        return 2;
                    }

                    Console.WriteLine("PASS: RemoteClient connected and pinged an active local EPLAN 2.9 server.");
                    Console.WriteLine("PASS: RemoteClient SynchronousMode = " + adapter.SynchronousMode);

                    var status = await Execute(gateway, Request("system.status")).ConfigureAwait(false);
                    if (!status.Success)
                    {
                        PrintFailure("system.status", status);
                        return 3;
                    }
                    Console.WriteLine("PASS: system.status via RemoteClient");

                    var active = await Execute(gateway, Request("project.getActive")).ConfigureAwait(false);
                    if (!active.Success)
                    {
                        PrintFailure("project.getActive", active);
                        return 4;
                    }

                    var project = Deserialize<ActiveProjectData>(active.DataJson);
                    if (project.ProjectSessionId == Guid.Empty || project.DatabaseIdentifier == 0)
                    {
                        Console.Error.WriteLine("FAIL: project.getActive returned an invalid session or database identifier: " + active.DataJson);
                        return 5;
                    }

                    Console.WriteLine("PASS: project.getActive via persistent RemoteClient session");
                    Console.WriteLine("  projectSessionId : " + project.ProjectSessionId.ToString("D"));
                    Console.WriteLine("  databaseId       : " + project.DatabaseIdentifier.ToString(CultureInfo.InvariantCulture));
                    Console.WriteLine("  name             : " + project.Name);
                    Console.WriteLine("  pageCount        : " + project.PageCount.ToString(CultureInfo.InvariantCulture));

                    var pagesRequest = Request("project.listPages");
                    pagesRequest.ProjectSessionId = project.ProjectSessionId;
                    var pages = await Execute(gateway, pagesRequest).ConfigureAwait(false);
                    if (!pages.Success)
                    {
                        PrintFailure("project.listPages", pages);
                        return 6;
                    }
                    Console.WriteLine("PASS: project.listPages used the same projectSessionId");
                    Console.WriteLine("  pages: " + pages.DataJson);

                    var selectionRequest = Request("selection.get");
                    selectionRequest.ProjectSessionId = project.ProjectSessionId;
                    var selection = await Execute(gateway, selectionRequest).ConfigureAwait(false);
                    if (!selection.Success)
                    {
                        PrintFailure("selection.get", selection);
                        return 7;
                    }
                    Console.WriteLine("PASS: selection.get used the same projectSessionId");
                    Console.WriteLine("  selection: " + selection.DataJson);

                    var selectionData = Deserialize<SelectionData>(selection.DataJson);
                    var functionItem = FindFirstFunction(selectionData);
                    if (functionItem == null || functionItem.Reference == null)
                    {
                        Console.WriteLine("INFO: no Function is selected; object.get runtime probe was skipped.");
                        Console.WriteLine("PASS: Task 6 persistent RemoteClient read sequence completed through selection.get.");
                        return 0;
                    }

                    var objectRequest = Request("object.get");
                    objectRequest.ProjectSessionId = project.ProjectSessionId;
                    objectRequest.Arguments["databaseIdentifier"] = functionItem.Reference.DatabaseIdentifier.ToString(CultureInfo.InvariantCulture);
                    objectRequest.Arguments["typeIdentifier"] = functionItem.Reference.TypeIdentifier.ToString(CultureInfo.InvariantCulture);
                    objectRequest.Arguments["objectIdentifier"] = functionItem.Reference.ObjectIdentifier.ToString(CultureInfo.InvariantCulture);
                    objectRequest.Arguments["stringIdentifier"] = functionItem.Reference.StringIdentifier;

                    var objectResponse = await Execute(gateway, objectRequest).ConfigureAwait(false);
                    if (!objectResponse.Success)
                    {
                        PrintFailure("object.get", objectResponse);
                        return 8;
                    }

                    Console.WriteLine("PASS: object.get resolved the selected Function in the same project session");
                    Console.WriteLine("  object: " + objectResponse.DataJson);
                    Console.WriteLine("PASS: Task 6 persistent RemoteClient read sequence completed.");
                    return 0;
                }
                finally
                {
                    gateway.Disconnect();
                }
            }
        }

        private static async Task<BridgeResponse> Execute(RemoteGateway gateway, BridgeRequest request)
        {
            Console.WriteLine("Executing " + request.Operation + " request " + request.RequestId.ToString("D"));
            return await gateway.ExecuteBridgeRequestAsync(request, OperationTimeout, CancellationToken.None).ConfigureAwait(false);
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

        private static SelectionItemData FindFirstFunction(SelectionData selection)
        {
            if (selection == null || selection.Items == null)
            {
                return null;
            }

            foreach (var item in selection.Items)
            {
                if (item != null && string.Equals(item.Kind, "Function", StringComparison.Ordinal) && item.Reference != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("Bridge response DataJson is empty.");
            }

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        private static void PrintFailure(string operation, BridgeResponse response)
        {
            Console.Error.WriteLine(
                "FAIL: " + operation + " -> " + response.ErrorCode + " " + response.ErrorMessage);
        }

        [DataContract]
        private sealed class ActiveProjectData
        {
            [DataMember(Name = "projectSessionId")]
            public Guid ProjectSessionId { get; set; }

            [DataMember(Name = "databaseIdentifier")]
            public uint DatabaseIdentifier { get; set; }

            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "pageCount")]
            public int PageCount { get; set; }
        }

        [DataContract]
        private sealed class SelectionData
        {
            [DataMember(Name = "items")]
            public SelectionItemData[] Items { get; set; }
        }

        [DataContract]
        private sealed class SelectionItemData
        {
            [DataMember(Name = "kind")]
            public string Kind { get; set; }

            [DataMember(Name = "reference")]
            public ObjectReferenceData Reference { get; set; }
        }

        [DataContract]
        private sealed class ObjectReferenceData
        {
            [DataMember(Name = "databaseIdentifier")]
            public uint DatabaseIdentifier { get; set; }

            [DataMember(Name = "typeIdentifier")]
            public uint TypeIdentifier { get; set; }

            [DataMember(Name = "objectIdentifier")]
            public long ObjectIdentifier { get; set; }

            [DataMember(Name = "stringIdentifier")]
            public string StringIdentifier { get; set; }
        }
    }
}
