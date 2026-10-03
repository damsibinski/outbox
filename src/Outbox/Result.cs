namespace Outbox;

public abstract class Result
{
    public abstract bool IsSuccess { get; }
    public abstract bool IsFailure { get; }
}
