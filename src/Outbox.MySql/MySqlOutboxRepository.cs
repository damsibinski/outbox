using System.Data;
using MySqlConnector;

namespace Outbox.MySql;

public sealed class MySqlOutboxRepository(string connectionString)
    : IOutboxRepository
{
    public async Task EnsureSchema(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = MySqlVars.Schema.EnsureOutboxMessagesTable;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureOutboxMessagesIndexAsync(connection, cancellationToken);
    }

    private static async Task EnsureOutboxMessagesIndexAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = MySqlVars.Schema.OutboxMessagesIndexExists;
            var indexCount = Convert.ToInt32(
                await existsCommand.ExecuteScalarAsync(cancellationToken));

            if (indexCount > 0)
                return;
        }

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = MySqlVars.Schema.CreateOutboxMessagesIndex;
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IOutboxUnitOfWork> BeginUnitOfWorkAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction =
                (MySqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            return new MySqlOutboxUnitOfWork(
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

        if (transaction is not MySqlTransaction mySqlTransaction)
            throw new ArgumentException(
                $"Expected a {nameof(MySqlTransaction)}, but received {transaction.GetType().Name}.",
                nameof(transaction));

        var connection = mySqlTransaction.Connection
            ?? throw new InvalidOperationException(
                "The MySQL transaction is not associated with a connection.");

        await MySqlOutboxUnitOfWork.InsertAsync(
            connection,
            mySqlTransaction,
            payload,
            cancellationToken);
    }
}
