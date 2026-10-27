using System;
using Audeu.EplanAi.AddIn.ReadModel;
using Audeu.EplanAi.Contracts;
using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;

namespace Audeu.EplanAi.AddIn.WriteModel
{
    public interface IWriteExecutionHook
    {
        void AfterAssignment(Function function);
    }

    public sealed class EplanWriteAdapter : IEplanWriteAdapter
    {
        private readonly IWriteExecutionHook _hook;

        public EplanWriteAdapter()
            : this(null)
        {
        }

        public EplanWriteAdapter(IWriteExecutionHook hook)
        {
            _hook = hook;
        }

        public ProjectSnapshot GetActiveProject()
        {
            return new EplanReadAdapter().GetActiveProject();
        }

        public WriteExecutionResult ApplyFunctionText(FunctionTextWriteCommand command)
        {
            if (command == null)
            {
                return WriteExecutionResult.Error(
                    BridgeErrorCodes.ValidationFailed,
                    "Function Text write command is required.");
            }

            try
            {
                Project project = new SelectionSet().GetCurrentProject(false);
                if (project == null || !project.IsValid)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.NoActiveProject,
                        "No active EPLAN project is available.");
                }

                if (project.DatabaseIdentifier != command.DatabaseIdentifier)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ObjectInvalid,
                        "The active project does not match the previewed Function reference.");
                }

                StorableObject resolved = StorableObject.FromStringIdentifier(command.StringIdentifier);
                if (resolved == null)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ObjectNotFound,
                        "The previewed Function could not be resolved.");
                }

                if (!resolved.IsValid ||
                    resolved.DatabaseIdentifier != command.DatabaseIdentifier ||
                    resolved.TypeIdentifier != command.TypeIdentifier ||
                    resolved.ObjectIdentifier != command.ObjectIdentifier)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ObjectInvalid,
                        "The resolved object no longer matches the previewed Function identity.");
                }

                var function = resolved as Function;
                if (function == null)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ObjectTypeNotAllowed,
                        "Only EPLAN Function objects may be changed.");
                }

                if (function.Project == null ||
                    !function.Project.IsValid ||
                    function.Project.DatabaseIdentifier != command.DatabaseIdentifier)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ObjectInvalid,
                        "The resolved Function is not attached to the previewed project.");
                }

                if (project.IsReadOnly || function.IsReadOnly)
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.ReadOnlyProject,
                        "The active project or Function is read-only.");
                }

                string currentValue = ReadDisplayedFunctionText(project, function);
                if (!string.Equals(currentValue, command.ExpectedCurrentValue, StringComparison.Ordinal))
                {
                    return WriteExecutionResult.Error(
                        BridgeErrorCodes.PlanStale,
                        "Function Text changed after preview; a fresh preview is required.");
                }

                using (UndoStep undoStep = new UndoManager().CreateUndoStep())
                using (SafetyPoint safetyPoint = SafetyPoint.Create())
                using (Transaction transaction = new TransactionManager().CreateTransaction())
                {
                    using (var text = new MultiLangString())
                    {
                        text.SetAsString(command.NewValue);
                        function.Properties.FUNC_TEXT.Set(text);
                    }

                    if (_hook != null)
                    {
                        _hook.AfterAssignment(function);
                    }

                    string readBack = ReadDisplayedFunctionText(project, function);
                    if (!string.Equals(readBack, command.NewValue, StringComparison.Ordinal))
                    {
                        return WriteExecutionResult.Error(
                            BridgeErrorCodes.EplanActionFailed,
                            "Function Text post-write verification failed.");
                    }

                    if (transaction.IsImplicitEplanTransactionCommited)
                    {
                        return WriteExecutionResult.Error(
                            BridgeErrorCodes.EplanActionFailed,
                            "EPLAN committed an implicit transaction during the guarded write.");
                    }

                    transaction.Commit();
                    safetyPoint.Commit();
                }

                return WriteExecutionResult.Ok();
            }
            catch (Exception)
            {
                return WriteExecutionResult.Error(
                    BridgeErrorCodes.EplanActionFailed,
                    "EPLAN rejected or could not safely complete the Function Text write.");
            }
        }

        private static string ReadDisplayedFunctionText(Project project, Function function)
        {
            PropertyValue propertyValue = function.Properties.FUNC_TEXT;
            MultiLangString multiLanguageValue = propertyValue.ToMultiLangString();
            var translate = new Translate();
            try
            {
                return translate.GetStringToDisplayInDialogs(project, multiLanguageValue);
            }
            finally
            {
                translate.Dispose();
                if (multiLanguageValue != null)
                {
                    multiLanguageValue.Dispose();
                }
            }
        }
    }
}
