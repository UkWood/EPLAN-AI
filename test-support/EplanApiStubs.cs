using System.Collections.Generic;

namespace Eplan.EplApi.ApplicationFramework
{
    public interface IEplAddIn
    {
        bool OnRegister(ref bool bLoadOnStart);
        bool OnUnregister();
        bool OnInit();
        bool OnInitGui();
        bool OnExit();
    }

    public interface IEplAction
    {
        bool Execute(ActionCallingContext oActionCallingContext);
        bool OnRegister(ref string Name, ref int Ordinal);
        void GetActionProperties(ref ActionProperties actionProperties);
    }

    public sealed class ActionProperties
    {
        public string Description { get; set; }
    }

    public class ActionCallingContext
    {
        private readonly Dictionary<string, string> _parameters = new Dictionary<string, string>();

        public void AddParameter(string name, string value)
        {
            _parameters[name] = value;
        }

        public void GetParameter(string name, ref string value)
        {
            string found;
            value = _parameters.TryGetValue(name, out found) ? found : null;
        }
    }
}
