using System.Data;
using System.Data.Common;
using Outbox.DependencyInjection;

namespace Outbox.IntegrationTests.Infrastructure;

public interface IOutboxDatabaseFixture
{
    string ConnectionString { get; }

    IOutboxRepository Repository { get; }

    void ConfigureOutbox(OutboxConfiguration configuration);

    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);

    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);

    Task<int> CountMessagesAsync(CancellationToken cancellationToken = default);

    Task ExecuteInCommittedTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default);

    Task ExecuteInRolledBackTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default);
}
