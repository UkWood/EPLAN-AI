namespace Audeu.EplanAi.Contracts
{
    public static class BridgeErrorCodes
    {
        public const string ProtocolMismatch = "PROTOCOL_MISMATCH";
        public const string NoActiveProject = "NO_ACTIVE_PROJECT";
        public const string SessionStale = "SESSION_STALE";
        public const string ObjectNotFound = "OBJECT_NOT_FOUND";
        public const string ObjectInvalid = "OBJECT_INVALID";
        public const string ObjectTypeNotAllowed = "OBJECT_TYPE_NOT_ALLOWED";
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string PlanAlreadyApplied = "PLAN_ALREADY_APPLIED";
        public const string BridgeUnknownOperation = "BRIDGE_UNKNOWN_OPERATION";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
