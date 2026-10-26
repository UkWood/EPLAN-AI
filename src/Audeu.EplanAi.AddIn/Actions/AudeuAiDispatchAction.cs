using System;
using System.IO;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.AddIn.Serialization;
using Audeu.EplanAi.Contracts;
using Eplan.EplApi.ApplicationFramework;

namespace Audeu.EplanAi.AddIn.Actions
{
    public sealed class AudeuAiDispatchAction : IEplAction
    {
        public const string ActionName = "AudeuAiDispatch";
        public const string RequestIdParameter = "REQUESTID";
        public const int ActionOrdinal = 20;

        private static readonly string DiagnosticsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Audeu",
            "EplanAiBridge",
            "diagnostics");

        private static readonly string ActionLogPath = Path.Combine(
            DiagnosticsDirectory,
            "dispatch-action.log");

        private readonly SpoolCodec _spool;
        private readonly RequestDispatcher _dispatcher;

        public AudeuAiDispatchAction()
            : this(SpoolCodec.CreateDefault(), new RequestDispatcher())
        {
            WriteDiagnostic("Constructor");
        }

        public AudeuAiDispatchAction(SpoolCodec spool, RequestDispatcher dispatcher)
        {
            _spool = spool ?? throw new ArgumentNullException(nameof(spool));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool Execute(ActionCallingContext context)
        {
            WriteDiagnostic("Execute:entered");

            if (context == null)
            {
                WriteDiagnostic("Execute:null-context");
                return false;
            }

            string requestIdText = null;
            context.GetParameter(RequestIdParameter, ref requestIdText);
            WriteDiagnostic("Execute:request-id=" + (requestIdText ?? "<null>"));

            Guid requestId;
            if (!Guid.TryParseExact(requestIdText, "D", out requestId))
            {
                WriteDiagnostic("Execute:invalid-request-id");
                return false;
            }

            try
            {
                var request = _spool.ReadRequest(requestId);
                var response = _dispatcher.Dispatch(request);
                _spool.WriteResponse(response);
                WriteDiagnostic("Execute:success");
                return true;
            }
            catch (Exception exception)
            {
                WriteDiagnostic("Execute:error=" + exception.GetType().Name + ":" + exception.Message);
                try
                {
                    _spool.WriteResponse(new BridgeResponse
                    {
                        RequestId = requestId,
                        Success = false,
                        ErrorCode = BridgeErrorCodes.InternalError,
                        ErrorMessage = exception.GetType().Name + ": " + exception.Message
                    });
                    WriteDiagnostic("Execute:error-response-written");
                    return true;
                }
                catch (Exception responseException)
                {
                    WriteDiagnostic("Execute:error-response-failed=" + responseException.GetType().Name + ":" + responseException.Message);
                    return false;
                }
            }
        }

        public bool OnRegister(ref string name, ref int ordinal)
        {
            WriteDiagnostic("OnRegister:entered");
            name = ActionName;
            ordinal = ActionOrdinal;
            WriteDiagnostic("OnRegister:name=" + name + ";ordinal=" + ordinal);
            return true;
        }

        public void GetActionProperties(ref ActionProperties actionProperties)
        {
            WriteDiagnostic("GetActionProperties");
            // EPLAN requires this interface member, but the verified 2.9.4
            // ActionProperties type exposes no Description property.
            // No metadata is required for bridge execution, so keep this a no-op.
        }

        private static void WriteDiagnostic(string eventName)
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsDirectory);
                var line = string.Join(
                    "|",
                    DateTime.UtcNow.ToString("O"),
                    eventName,
                    typeof(AudeuAiDispatchAction).Assembly.Location);
                File.AppendAllText(ActionLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never prevent EPLAN from registering or executing the action.
            }
        }
    }
}
