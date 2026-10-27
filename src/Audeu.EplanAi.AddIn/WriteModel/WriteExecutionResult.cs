namespace Audeu.EplanAi.AddIn.WriteModel
{
    public sealed class WriteExecutionResult
    {
        private WriteExecutionResult(bool success, string errorCode, string errorMessage)
        {
            Success = success;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        public static WriteExecutionResult Ok()
        {
            return new WriteExecutionResult(true, null, null);
        }

        public static WriteExecutionResult Error(string errorCode, string errorMessage)
        {
            return new WriteExecutionResult(false, errorCode, errorMessage);
        }
    }
}
