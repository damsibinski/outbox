namespace Outbox;

public interface IOutboxHandler
{
    Task<Result> HandleAsync<T>(T payload, IDictionary<string, string> headers);
}
