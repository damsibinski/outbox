using System.Data;

namespace Outbox;

public interface IOutboxRepository
{
    Task EnsureSchema(CancellationToken cancellationToken);

    Task EnsureSchema() => EnsureSchema(CancellationToken.None);

    Task<IOutboxUnitOfWork> BeginUnitOfWorkAsync(CancellationToken cancellationToken);

    Task AddAsync(
        Payload payload,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}
