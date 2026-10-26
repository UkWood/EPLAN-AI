namespace Audeu.EplanAi.Contracts
{
    public static class BridgeErrorCodes
    {
        public const string ProtocolMismatch = "PROTOCOL_MISMATCH";
        public const string SessionStale = "SESSION_STALE";
        public const string PlanAlreadyApplied = "PLAN_ALREADY_APPLIED";
        public const string BridgeUnknownOperation = "BRIDGE_UNKNOWN_OPERATION";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
