using System;
using System.Collections.Generic;
using System.Globalization;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.Contracts;

namespace Audeu.EplanAi.AddIn.WriteModel
{
    public sealed class WriteOperationService
    {
        private const int MaxFunctionTextLength = 1024;
        private const int ApplyArgumentCount = 7;

        private readonly IEplanWriteAdapter _adapter;
        private readonly ProjectSessionTracker _sessions;
        private readonly object _sync = new object();
        private readonly HashSet<Guid> _claimedPlanIds = new HashSet<Guid>();
        private LastAppliedChange _lastChange;

        public WriteOperationService(IEplanWriteAdapter adapter, ProjectSessionTracker sessions)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        public BridgeResponse Apply(BridgeRequest request)
        {
            ProjectSnapshot project;
            BridgeResponse guard = RequireWritableCurrentSession(request, true, out project);
            if (guard != null)
            {
                return guard;
            }

            Guid changePlanId;
            uint databaseIdentifier;
            uint typeIdentifier;
            long objectIdentifier;
            string stringIdentifier;
            string oldValue;
            string newValue;
            if (!TryReadApplyArguments(
                request,
                out changePlanId,
                out databaseIdentifier,
                out typeIdentifier,
                out objectIdentifier,
                out stringIdentifier,
                out oldValue,
                out newValue))
            {
                return Error(request, BridgeErrorCodes.ValidationFailed, "Change plan arguments are missing or malformed.");
            }

            if (databaseIdentifier != project.DatabaseIdentifier)
            {
                return Error(request, BridgeErrorCodes.SessionStale, "The change plan belongs to a stale project session.");
            }

            lock (_sync)
            {
                if (!_claimedPlanIds.Add(changePlanId))
                {
                    return Error(request, BridgeErrorCodes.PlanAlreadyApplied, "The change plan has already been claimed for application.");
                }
            }

            var command = new FunctionTextWriteCommand(
                databaseIdentifier,
                typeIdentifier,
                objectIdentifier,
                stringIdentifier,
                oldValue,
                newValue,
                false);

            WriteExecutionResult result = ExecuteAdapter(command);
            if (!result.Success)
            {
                return WriteError(request, result);
            }

            lock (_sync)
            {
                _lastChange = new LastAppliedChange(
                    request.ProjectSessionId.Value,
                    databaseIdentifier,
                    typeIdentifier,
                    objectIdentifier,
                    stringIdentifier,
                    oldValue,
                    newValue);
            }

            return Success(request, "{\"status\":\"applied\"}");
        }

        public BridgeResponse UndoLast(BridgeRequest request)
        {
            ProjectSnapshot project;
            BridgeResponse guard = RequireWritableCurrentSession(request, false, out project);
            if (guard != null)
            {
                return guard;
            }

            if (request.Arguments == null || request.Arguments.Count != 0)
            {
                return Error(request, BridgeErrorCodes.ValidationFailed, "Undo does not accept caller-controlled arguments.");
            }

            LastAppliedChange snapshot;
            lock (_sync)
            {
                snapshot = _lastChange;
            }

            if (snapshot == null)
            {
                return Error(request, BridgeErrorCodes.ValidationFailed, "There is no AI Function Text change available to undo.");
            }

            if (snapshot.ProjectSessionId != request.ProjectSessionId.Value
                || snapshot.DatabaseIdentifier != project.DatabaseIdentifier)
            {
                return Error(request, BridgeErrorCodes.PlanStale, "The last AI change belongs to a stale project session.");
            }

            var command = new FunctionTextWriteCommand(
                snapshot.DatabaseIdentifier,
                snapshot.TypeIdentifier,
                snapshot.ObjectIdentifier,
                snapshot.StringIdentifier,
                snapshot.NewValue,
                snapshot.OldValue,
                true);

            WriteExecutionResult result = ExecuteAdapter(command);
            if (!result.Success)
            {
                return WriteError(request, result);
            }

            lock (_sync)
            {
                if (ReferenceEquals(_lastChange, snapshot))
                {
                    _lastChange = null;
                }
            }

            return Success(request, "{\"status\":\"undone\"}");
        }

        private BridgeResponse RequireWritableCurrentSession(
            BridgeRequest request,
            bool allowApplyArguments,
            out ProjectSnapshot project)
        {
            project = null;
            if (request == null || !string.Equals(request.Mode, "write", StringComparison.Ordinal))
            {
                return Error(request, BridgeErrorCodes.ValidationFailed, "Write operations require mode 'write'.");
            }

            project = _adapter.GetActiveProject();
            if (project == null)
            {
                _sessions.Clear();
                lock (_sync)
                {
                    _lastChange = null;
                }

                return Error(request, BridgeErrorCodes.NoActiveProject, "No active EPLAN project is available.");
            }

            if (!request.ProjectSessionId.HasValue
                || !_sessions.IsCurrent(request.ProjectSessionId.Value, project.DatabaseIdentifier))
            {
                return Error(request, BridgeErrorCodes.SessionStale, "The project session is missing or stale.");
            }

            if (project.IsReadOnly)
            {
                return Error(request, BridgeErrorCodes.ReadOnlyProject, "The active EPLAN project is read-only.");
            }

            return null;
        }

        private static bool TryReadApplyArguments(
            BridgeRequest request,
            out Guid changePlanId,
            out uint databaseIdentifier,
            out uint typeIdentifier,
            out long objectIdentifier,
            out string stringIdentifier,
            out string oldValue,
            out string newValue)
        {
            changePlanId = Guid.Empty;
            databaseIdentifier = 0;
            typeIdentifier = 0;
            objectIdentifier = 0;
            stringIdentifier = null;
            oldValue = null;
            newValue = null;

            if (request.Arguments == null || request.Arguments.Count != ApplyArgumentCount)
            {
                return false;
            }

            string planIdText;
            string databaseText;
            string typeText;
            string objectText;
            return request.Arguments.TryGetValue("changePlanId", out planIdText)
                && request.Arguments.TryGetValue("databaseIdentifier", out databaseText)
                && request.Arguments.TryGetValue("typeIdentifier", out typeText)
                && request.Arguments.TryGetValue("objectIdentifier", out objectText)
                && request.Arguments.TryGetValue("stringIdentifier", out stringIdentifier)
                && request.Arguments.TryGetValue("oldValue", out oldValue)
                && request.Arguments.TryGetValue("newValue", out newValue)
                && Guid.TryParseExact(planIdText, "D", out changePlanId)
                && uint.TryParse(databaseText, NumberStyles.None, CultureInfo.InvariantCulture, out databaseIdentifier)
                && uint.TryParse(typeText, NumberStyles.None, CultureInfo.InvariantCulture, out typeIdentifier)
                && long.TryParse(objectText, NumberStyles.Integer, CultureInfo.InvariantCulture, out objectIdentifier)
                && !string.IsNullOrWhiteSpace(stringIdentifier)
                && oldValue != null
                && newValue != null
                && newValue.Length <= MaxFunctionTextLength;
        }

        private WriteExecutionResult ExecuteAdapter(FunctionTextWriteCommand command)
        {
            try
            {
                return _adapter.ApplyFunctionText(command)
                    ?? WriteExecutionResult.Error(BridgeErrorCodes.EplanActionFailed, "EPLAN returned no write result.");
            }
            catch (Exception)
            {
                return WriteExecutionResult.Error(BridgeErrorCodes.EplanActionFailed, "The EPLAN Function Text write failed.");
            }
        }

        private static BridgeResponse WriteError(BridgeRequest request, WriteExecutionResult result)
        {
            string code = IsStableWriteError(result.ErrorCode)
                ? result.ErrorCode
                : BridgeErrorCodes.EplanActionFailed;
            string message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "The EPLAN Function Text write failed."
                : result.ErrorMessage;
            return Error(request, code, message);
        }

        private static bool IsStableWriteError(string code)
        {
            return code == BridgeErrorCodes.NoActiveProject
                || code == BridgeErrorCodes.SessionStale
                || code == BridgeErrorCodes.ObjectNotFound
                || code == BridgeErrorCodes.ObjectInvalid
                || code == BridgeErrorCodes.ObjectTypeNotAllowed
                || code == BridgeErrorCodes.ReadOnlyProject
                || code == BridgeErrorCodes.PlanStale
                || code == BridgeErrorCodes.ValidationFailed
                || code == BridgeErrorCodes.EplanActionFailed;
        }

        private static BridgeResponse Success(BridgeRequest request, string dataJson)
        {
            return new BridgeResponse
            {
                RequestId = request == null ? Guid.Empty : request.RequestId,
                Success = true,
                DataJson = dataJson
            };
        }

        private static BridgeResponse Error(BridgeRequest request, string code, string message)
        {
            return new BridgeResponse
            {
                RequestId = request == null ? Guid.Empty : request.RequestId,
                Success = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }

        private sealed class LastAppliedChange
        {
            public LastAppliedChange(
                Guid projectSessionId,
                uint databaseIdentifier,
                uint typeIdentifier,
                long objectIdentifier,
                string stringIdentifier,
                string oldValue,
                string newValue)
            {
                ProjectSessionId = projectSessionId;
                DatabaseIdentifier = databaseIdentifier;
                TypeIdentifier = typeIdentifier;
                ObjectIdentifier = objectIdentifier;
                StringIdentifier = stringIdentifier;
                OldValue = oldValue;
                NewValue = newValue;
            }

            public Guid ProjectSessionId { get; }
            public uint DatabaseIdentifier { get; }
            public uint TypeIdentifier { get; }
            public long ObjectIdentifier { get; }
            public string StringIdentifier { get; }
            public string OldValue { get; }
            public string NewValue { get; }
        }
    }
}
