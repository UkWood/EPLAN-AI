using System;
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

    // Keep this stub intentionally minimal and aligned with the public members
    // exposed by the target EPLAN 2.9.4 ActionProperties assembly used by the
    // verified workstation. Do not add documentation-example-only members here.
    public sealed class ActionProperties
    {
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

namespace Eplan.EplApi.DataModel
{
    public class PropertyValue
    {
        public PropertyValue()
        {
        }

        public PropertyValue(string value)
        {
            Value = value;
        }

        public string Value { get; set; }

        public override string ToString()
        {
            return Value;
        }
    }

    public class FunctionPropertyList
    {
        public PropertyValue FUNC_TEXT { get; set; } = new PropertyValue();
        public PropertyValue FUNC_IDENTDEVICETAG { get; set; } = new PropertyValue();
    }

    public class StorableObject
    {
        public static Func<string, StorableObject> StubResolver { get; set; }

        public uint DatabaseIdentifier { get; set; }
        public uint TypeIdentifier { get; set; }
        public long ObjectIdentifier { get; set; }
        public bool IsValid { get; set; } = true;
        public Project Project { get; set; }
        public string StubTypeName { get; set; }
        public string StubStringIdentifier { get; set; }

        public virtual string GetTypeName()
        {
            return string.IsNullOrWhiteSpace(StubTypeName) ? GetType().Name : StubTypeName;
        }

        public string ToStringIdentifier()
        {
            return StubStringIdentifier;
        }

        public static StorableObject FromStringIdentifier(string stringIdentifier)
        {
            return StubResolver == null ? null : StubResolver(stringIdentifier);
        }
    }

    public class Project : StorableObject
    {
        public string ProjectName { get; set; }
        public string ProjectFullName { get; set; }
        public bool IsReadOnly { get; set; }
        public Page[] Pages { get; set; } = new Page[0];
    }

    public class Page : StorableObject
    {
        public string Name { get; set; }
        public string PageTypeName { get; set; }
    }

    public class Placement : StorableObject
    {
        public virtual Page Page { get; set; }
    }

    public class Function : Placement
    {
        public string Name { get; set; }
        public FunctionPropertyList Properties { get; } = new FunctionPropertyList();
    }
}

namespace Eplan.EplApi.HEServices
{
    using Eplan.EplApi.DataModel;

    public class SelectionSet
    {
        public static Project StubCurrentProject { get; set; }
        public static StorableObject[] StubSelection { get; set; } = new StorableObject[0];

        public StorableObject[] Selection
        {
            get { return StubSelection ?? new StorableObject[0]; }
        }

        public Project GetCurrentProject(bool useSelectionDialog)
        {
            return StubCurrentProject;
        }
    }
}
