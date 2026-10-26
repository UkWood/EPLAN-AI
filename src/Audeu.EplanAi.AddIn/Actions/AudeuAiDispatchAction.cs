using System;
using Audeu.EplanAi.AddIn.Dispatch;
using Audeu.EplanAi.AddIn.Serialization;
using Eplan.EplApi.ApplicationFramework;

namespace Audeu.EplanAi.AddIn.Actions
{
    public sealed class AudeuAiDispatchAction : IEplAction
    {
        public const string ActionName = "AudeuAiDispatch";
        public const string RequestIdParameter = "REQUESTID";
        public const int ActionOrdinal = 20;

        private readonly SpoolCodec _spool;
        private readonly RequestDispatcher _dispatcher;

        public AudeuAiDispatchAction()
            : this(SpoolCodec.CreateDefault(), new RequestDispatcher())
        {
        }

        public AudeuAiDispatchAction(SpoolCodec spool, RequestDispatcher dispatcher)
        {
            _spool = spool ?? throw new ArgumentNullException(nameof(spool));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool Execute(ActionCallingContext context)
        {
            if (context == null)
            {
                return false;
            }

            string requestIdText = null;
            context.GetParameter(RequestIdParameter, ref requestIdText);

            Guid requestId;
            if (!Guid.TryParseExact(requestIdText, "D", out requestId))
            {
                return false;
            }

            try
            {
                var request = _spool.ReadRequest(requestId);
                var response = _dispatcher.Dispatch(request);
                _spool.WriteResponse(response);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool OnRegister(ref string name, ref int ordinal)
        {
            name = ActionName;
            ordinal = ActionOrdinal;
            return true;
        }

        public void GetActionProperties(ref ActionProperties actionProperties)
        {
            // EPLAN requires this interface member, but the verified 2.9.4
            // ActionProperties type exposes no Description property.
            // No metadata is required for bridge execution, so keep this a no-op.
        }
    }
}
