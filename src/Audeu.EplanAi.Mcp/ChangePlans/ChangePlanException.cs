using System;

namespace Audeu.EplanAi.Mcp.ChangePlans;

public sealed class ChangePlanException : Exception
{
    public ChangePlanException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
