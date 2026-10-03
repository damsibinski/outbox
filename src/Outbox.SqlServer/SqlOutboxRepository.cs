using System.Data;
using Microsoft.Data.SqlClient;

namespace Outbox.SqlServer;

public sealed class SqlOutboxRepository(string connectionString)
    : IOutboxRepository
{
    public async Task EnsureSchema(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerVars.Schema.EnsureOutboxMessages;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IOutboxUnitOfWork> BeginUnitOfWorkAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            return new SqlOutboxUnitOfWork(
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

        if (transaction is not SqlTransaction sqlTransaction)
            throw new ArgumentException(
                $"Expected a {nameof(SqlTransaction)}, but received {transaction.GetType().Name}.",
                nameof(transaction));

        var connection = sqlTransaction.Connection
            ?? throw new InvalidOperationException(
                "The SQL Server transaction is not associated with a connection.");

        await SqlOutboxUnitOfWork.InsertAsync(
            connection,
            sqlTransaction,
            payload,
            cancellationToken);
    }
}
