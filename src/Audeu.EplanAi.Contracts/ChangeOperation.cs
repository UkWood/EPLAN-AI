namespace Audeu.EplanAi.Contracts
{
    public sealed class ChangeOperation
    {
        public ChangeOperation(string kind, ObjectReference target, string oldValue, string newValue)
        {
            Kind = kind;
            Target = target;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string Kind { get; }

        public ObjectReference Target { get; }

        public string OldValue { get; }

        public string NewValue { get; }
    }
}
