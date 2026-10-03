namespace Outbox;

public interface IOutboxUnitOfWork : IAsyncDisposable
{
    Task AddAsync(Payload payload, CancellationToken cancellationToken);

    Task<OutboxMessage?> ClaimNextAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
