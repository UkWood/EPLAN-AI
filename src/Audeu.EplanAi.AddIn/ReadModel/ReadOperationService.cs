using System;
using System.Collections.Generic;
using System.Globalization;
using Audeu.EplanAi.AddIn.Serialization;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.ReadModel
{
    public sealed class ReadOperationService
    {
        private readonly IEplanReadAdapter _adapter;
        private readonly ProjectSessionTracker _sessions;

        public ReadOperationService(IEplanReadAdapter adapter, ProjectSessionTracker sessions)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        public BridgeResponse GetActiveProject(BridgeRequest request)
        {
            var project = _adapter.GetActiveProject();
            if (project == null)
            {
                _sessions.Clear();
                return Error(request, BridgeErrorCodes.NoActiveProject, "No active EPLAN project is available.");
            }

            var sessionId = _sessions.GetOrCreate(project.DatabaseIdentifier);
            return Success(request, CompactJson.Serialize(new ActiveProjectResult
            {
                ProjectSessionId = sessionId,
                DatabaseIdentifier = project.DatabaseIdentifier,
                Name = project.Name,
                FullName = project.FullName,
                PageCount = project.PageCount,
                IsReadOnly = project.IsReadOnly
            }));
        }

        public BridgeResponse ListPages(BridgeRequest request)
        {
            ProjectSnapshot project;
            Guid sessionId;
            var guard = RequireCurrentSession(request, out project, out sessionId);
            if (guard != null)
            {
                return guard;
            }

            var snapshots = _adapter.GetPages(project.DatabaseIdentifier) ?? new List<PageSnapshot>();
            var items = new List<PageItemResult>(snapshots.Count);
            foreach (var page in snapshots)
            {
                items.Add(new PageItemResult
                {
                    Ordinal = page.Ordinal,
                    Name = page.Name,
                    PageType = page.PageType
                });
            }

            return Success(request, CompactJson.Serialize(new PageListResult
            {
                ProjectSessionId = sessionId,
                Items = items
            }));
        }

        public BridgeResponse GetSelection(BridgeRequest request)
        {
            ProjectSnapshot project;
            Guid sessionId;
            var guard = RequireCurrentSession(request, out project, out sessionId);
            if (guard != null)
            {
                return guard;
            }

            var snapshots = _adapter.GetSelection(project.DatabaseIdentifier) ?? new List<ObjectSnapshot>();
            var items = new List<SelectionItemResult>();
            foreach (var item in snapshots)
            {
                if (item == null || !item.IsValid || item.DatabaseIdentifier != project.DatabaseIdentifier)
                {
                    continue;
                }

                items.Add(new SelectionItemResult
                {
                    Kind = item.Kind,
                    Reference = Reference(sessionId, item),
                    Name = item.Name,
                    Page = item.Page
                });
            }

            return Success(request, CompactJson.Serialize(new SelectionResult
            {
                ProjectSessionId = sessionId,
                Items = items
            }));
        }

        public BridgeResponse GetObject(BridgeRequest request)
        {
            ProjectSnapshot project;
            Guid sessionId;
            var guard = RequireCurrentSession(request, out project, out sessionId);
            if (guard != null)
            {
                return guard;
            }

            uint databaseIdentifier;
            uint typeIdentifier;
            long objectIdentifier;
            string stringIdentifier;
            if (!TryReadObjectReference(request, out databaseIdentifier, out typeIdentifier, out objectIdentifier, out stringIdentifier))
            {
                return Error(request, BridgeErrorCodes.ValidationFailed, "Object reference arguments are missing or malformed.");
            }

            if (databaseIdentifier != project.DatabaseIdentifier)
            {
                return Error(request, BridgeErrorCodes.SessionStale, "The object reference belongs to a stale project session.");
            }

            var resolved = _adapter.ResolveObject(databaseIdentifier, stringIdentifier);
            if (resolved == null)
            {
                return Error(request, BridgeErrorCodes.ObjectNotFound, "The referenced EPLAN object could not be resolved.");
            }

            if (!resolved.IsValid
                || resolved.DatabaseIdentifier != databaseIdentifier
                || resolved.TypeIdentifier != typeIdentifier
                || resolved.ObjectIdentifier != objectIdentifier)
            {
                return Error(request, BridgeErrorCodes.ObjectInvalid, "The resolved EPLAN object does not match the supplied reference.");
            }

            if (!resolved.IsFunction)
            {
                return Error(request, BridgeErrorCodes.ObjectTypeNotAllowed, "Task 5 object inspection allows Function objects only.");
            }

            return Success(request, CompactJson.Serialize(new FunctionResult
            {
                Kind = "Function",
                Reference = Reference(sessionId, resolved),
                Name = resolved.Name,
                Page = resolved.Page,
                FunctionText = resolved.FunctionText,
                DeviceTag = resolved.DeviceTag
            }));
        }

        private BridgeResponse RequireCurrentSession(BridgeRequest request, out ProjectSnapshot project, out Guid sessionId)
        {
            project = _adapter.GetActiveProject();
            sessionId = Guid.Empty;
            if (project == null)
            {
                _sessions.Clear();
                return Error(request, BridgeErrorCodes.NoActiveProject, "No active EPLAN project is available.");
            }

            sessionId = _sessions.GetOrCreate(project.DatabaseIdentifier);
            if (!request.ProjectSessionId.HasValue || !_sessions.IsCurrent(request.ProjectSessionId.Value, project.DatabaseIdentifier))
            {
                return Error(request, BridgeErrorCodes.SessionStale, "The project session is missing or stale.");
            }

            return null;
        }

        private static bool TryReadObjectReference(BridgeRequest request, out uint databaseIdentifier, out uint typeIdentifier, out long objectIdentifier, out string stringIdentifier)
        {
            databaseIdentifier = 0;
            typeIdentifier = 0;
            objectIdentifier = 0;
            stringIdentifier = null;

            if (request.Arguments == null)
            {
                return false;
            }

            string databaseText;
            string typeText;
            string objectText;
            return request.Arguments.TryGetValue("databaseIdentifier", out databaseText)
                && request.Arguments.TryGetValue("typeIdentifier", out typeText)
                && request.Arguments.TryGetValue("objectIdentifier", out objectText)
                && request.Arguments.TryGetValue("stringIdentifier", out stringIdentifier)
                && !string.IsNullOrWhiteSpace(stringIdentifier)
                && uint.TryParse(databaseText, NumberStyles.None, CultureInfo.InvariantCulture, out databaseIdentifier)
                && uint.TryParse(typeText, NumberStyles.None, CultureInfo.InvariantCulture, out typeIdentifier)
                && long.TryParse(objectText, NumberStyles.Integer, CultureInfo.InvariantCulture, out objectIdentifier);
        }

        private static ObjectReferenceResult Reference(Guid sessionId, ObjectSnapshot item)
        {
            return new ObjectReferenceResult
            {
                ProjectSessionId = sessionId,
                DatabaseIdentifier = item.DatabaseIdentifier,
                TypeIdentifier = item.TypeIdentifier,
                ObjectIdentifier = item.ObjectIdentifier,
                StringIdentifier = item.StringIdentifier
            };
        }

        private static BridgeResponse Success(BridgeRequest request, string dataJson)
        {
            return new BridgeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                DataJson = dataJson
            };
        }

        private static BridgeResponse Error(BridgeRequest request, string code, string message)
        {
            return new BridgeResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
