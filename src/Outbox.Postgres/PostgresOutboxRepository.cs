using System.Data;
using Npgsql;

namespace Outbox.Postgres;

public sealed class PostgresOutboxRepository(string connectionString)
    : IOutboxRepository
{
    public async Task EnsureSchema(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PostgresVars.Schema.EnsureOutboxMessages;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IOutboxUnitOfWork> BeginUnitOfWorkAsync(
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = await connection.BeginTransactionAsync(cancellationToken);

            return new PostgresOutboxUnitOfWork(
                connection,
                transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task AddAsync(
        Payload payload,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction is not NpgsqlTransaction postgresTransaction)
            throw new ArgumentException(
                $"Expected a {nameof(NpgsqlTransaction)}, but received {transaction.GetType().Name}.",
                nameof(transaction));

        var connection = postgresTransaction.Connection
            ?? throw new InvalidOperationException(
                "The PostgreSQL transaction is not associated with a connection.");

        await PostgresOutboxUnitOfWork.InsertAsync(
            connection,
            postgresTransaction,
            payload,
            cancellationToken);
    }
}
