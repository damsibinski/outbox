namespace Outbox;

public sealed class Failure : Result
{
    public Failure(string errorCode)
    {
        ErrorCode = errorCode;
        IsPermanent = true;
        IsTransient = false;
    }

    public Failure(string errorCode, bool isTransient)
    {
        ErrorCode = errorCode;
        IsPermanent = !isTransient;
        IsTransient = isTransient;
    }

    public override bool IsSuccess => false;
    public override bool IsFailure => true;
    public string ErrorCode { get; }
    public bool IsPermanent { get; }
    public bool IsTransient { get; }
}
