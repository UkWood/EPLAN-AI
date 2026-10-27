namespace Audeu.EplanAi.AddIn.WriteModel
{
    public sealed class FunctionTextWriteCommand
    {
        public FunctionTextWriteCommand(
            uint databaseIdentifier,
            uint typeIdentifier,
            long objectIdentifier,
            string stringIdentifier,
            string expectedCurrentValue,
            string newValue,
            bool isUndo)
        {
            DatabaseIdentifier = databaseIdentifier;
            TypeIdentifier = typeIdentifier;
            ObjectIdentifier = objectIdentifier;
            StringIdentifier = stringIdentifier;
            ExpectedCurrentValue = expectedCurrentValue;
            NewValue = newValue;
            IsUndo = isUndo;
        }

        public uint DatabaseIdentifier { get; }
        public uint TypeIdentifier { get; }
        public long ObjectIdentifier { get; }
        public string StringIdentifier { get; }
        public string ExpectedCurrentValue { get; }
        public string NewValue { get; }
        public bool IsUndo { get; }
    }
}
