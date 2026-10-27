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

namespace Eplan.EplApi.Base
{
    public sealed class MultiLangString : IDisposable
    {
        public MultiLangString()
        {
        }

        public MultiLangString(string internalString)
        {
            InternalString = internalString;
        }

        public string InternalString { get; private set; }

        public string StubDisplayString
        {
            get
            {
                const string languageIndependentPrefix = "??_??@";
                if (!string.IsNullOrEmpty(InternalString) &&
                    InternalString.StartsWith(languageIndependentPrefix, StringComparison.Ordinal) &&
                    InternalString.EndsWith(";", StringComparison.Ordinal))
                {
                    return InternalString.Substring(
                        languageIndependentPrefix.Length,
                        InternalString.Length - languageIndependentPrefix.Length - 1);
                }

                return InternalString;
            }
        }

        public void SetAsString(string value)
        {
            if (value == null)
            {
                InternalString = null;
                return;
            }

            InternalString = value.StartsWith("??_??@", StringComparison.Ordinal)
                ? value
                : "??_??@" + value + ";";
        }

        public void Dispose()
        {
        }
    }
}

namespace Eplan.EplApi.DataModel
{
    using Eplan.EplApi.Base;

    public static class StubWriteTrace
    {
        private static readonly List<string> s_events = new List<string>();

        public static IList<string> Events
        {
            get { return s_events.AsReadOnly(); }
        }

        public static bool TransactionImplicitCommit { get; set; }

        public static void Add(string value)
        {
            s_events.Add(value);
        }

        public static void Reset()
        {
            s_events.Clear();
            TransactionImplicitCommit = false;
            SafetyPoint.StubReset();
        }
    }

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

        public MultiLangString ToMultiLangString()
        {
            return new MultiLangString(Value);
        }

        public PropertyValue Set(MultiLangString value)
        {
            SafetyPoint.StubRecord(this, Value);
            Value = value == null ? null : value.InternalString;
            StubWriteTrace.Add("property.set");
            return this;
        }

        internal void StubRestore(string value)
        {
            Value = value;
        }

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
        public bool IsReadOnly { get; set; }
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

    public sealed class SafetyPoint : IDisposable
    {
        private static SafetyPoint s_current;
        private readonly Dictionary<PropertyValue, string> _originalValues =
            new Dictionary<PropertyValue, string>();
        private bool _committed;
        private bool _disposed;

        private SafetyPoint()
        {
        }

        public static SafetyPoint Create()
        {
            var point = new SafetyPoint();
            s_current = point;
            StubWriteTrace.Add("safety.create");
            return point;
        }

        internal static void StubRecord(PropertyValue property, string oldValue)
        {
            if (s_current != null && !s_current._originalValues.ContainsKey(property))
            {
                s_current._originalValues.Add(property, oldValue);
            }
        }

        internal static void StubReset()
        {
            s_current = null;
        }

        public void Commit()
        {
            _committed = true;
            StubWriteTrace.Add("safety.commit");
        }

        public void Rollback()
        {
            Restore();
            StubWriteTrace.Add("safety.rollback");
        }

        public void Start()
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (!_committed)
            {
                Restore();
                StubWriteTrace.Add("safety.rollback");
            }

            if (ReferenceEquals(s_current, this))
            {
                s_current = null;
            }

            _disposed = true;
            StubWriteTrace.Add("safety.dispose");
        }

        private void Restore()
        {
            foreach (KeyValuePair<PropertyValue, string> entry in _originalValues)
            {
                entry.Key.StubRestore(entry.Value);
            }
        }
    }

    public sealed class Transaction : IDisposable
    {
        public bool IsImplicitEplanTransactionCommited
        {
            get { return StubWriteTrace.TransactionImplicitCommit; }
        }

        public void Commit()
        {
            StubWriteTrace.Add("transaction.commit");
        }

        public void Abort()
        {
            StubWriteTrace.Add("transaction.abort");
        }

        public void Dispose()
        {
            StubWriteTrace.Add("transaction.dispose");
        }
    }

    public class TransactionManager
    {
        public Transaction CreateTransaction()
        {
            StubWriteTrace.Add("transaction.create");
            return new Transaction();
        }
    }

    public sealed class UndoStep : IDisposable
    {
        public void Dispose()
        {
            StubWriteTrace.Add("undo.dispose");
        }
    }

    public class UndoManager
    {
        public UndoStep CreateUndoStep()
        {
            StubWriteTrace.Add("undo.create");
            return new UndoStep();
        }
    }
}

namespace Eplan.EplApi.HEServices
{
    using Eplan.EplApi.Base;
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

    public sealed class Translate
    {
        public string GetStringToDisplayInDialogs(Project project, MultiLangString value)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return value == null ? null : value.StubDisplayString;
        }

        public void Dispose()
        {
        }
    }
}
