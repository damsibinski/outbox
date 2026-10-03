namespace Outbox;

public sealed class Success : Result
{
    public override bool IsSuccess => true;
    public override bool IsFailure => false;
}
