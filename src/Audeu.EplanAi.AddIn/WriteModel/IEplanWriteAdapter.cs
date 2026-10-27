using Audeu.EplanAi.AddIn.ReadModel;

namespace Audeu.EplanAi.AddIn.WriteModel
{
    public interface IEplanWriteAdapter
    {
        ProjectSnapshot GetActiveProject();
        WriteExecutionResult ApplyFunctionText(FunctionTextWriteCommand command);
    }
}
