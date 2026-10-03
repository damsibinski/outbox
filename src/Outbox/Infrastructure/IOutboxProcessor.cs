namespace Outbox.Infrastructure;

public interface IOutboxProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
