using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Outbox.SqlServer;

internal sealed class SqlOutboxUnitOfWork(
    SqlConnection connection,
    SqlTransaction transaction) : IOutboxUnitOfWork
{
    private bool _isCompleted;

    public Task AddAsync(Payload payload, CancellationToken cancellationToken)
    {
        return InsertAsync(connection, transaction, payload, cancellationToken);
    }

    internal static async Task InsertAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Payload payload,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SqlServerVars.Queries.InsertOutboxMessage;

        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.Id, SqlDbType.UniqueIdentifier)
            {
                Value = Guid.NewGuid()
            });
        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.Data, SqlDbType.NVarChar, -1)
            {
                Value = payload.Data
            });
        command.Parameters.Add(
            new SqlParameter(
                SqlServerVars.Parameters.Type,
                SqlDbType.NVarChar,
                SqlServerVars.TypeColumnMaxLength)
            {
                Value = payload.Type
            });
        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.Headers, SqlDbType.NVarChar, -1)
            {
                Value = JsonSerializer.Serialize(
                    payload.Headers,
                    Vars.Json.SerializerOptions)
            });
        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.InsertDate, SqlDbType.DateTime2)
            {
                Value = DateTime.UtcNow
            });
        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.ScheduledAt, SqlDbType.DateTimeOffset)
            {
                Value = payload.ScheduledAt
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OutboxMessage?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SqlServerVars.Queries.ClaimNextOutboxMessage;
        command.Parameters.Add(
            new SqlParameter(SqlServerVars.Parameters.Now, SqlDbType.DateTimeOffset)
            {
                Value = DateTimeOffset.UtcNow
            });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OutboxMessage(
            reader.GetGuid(reader.GetOrdinal(SqlServerVars.Columns.Id)),
            reader.GetString(reader.GetOrdinal(SqlServerVars.Columns.Data)),
            reader.GetString(reader.GetOrdinal(SqlServerVars.Columns.Type)),
            reader.GetString(reader.GetOrdinal(SqlServerVars.Columns.Headers)),
            reader.GetDateTime(reader.GetOrdinal(SqlServerVars.Columns.InsertDate)),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(SqlServerVars.Columns.ScheduledAt)));
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_isCompleted)
            return;

        await transaction.CommitAsync(cancellationToken);
        _isCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_isCompleted)
            return;

        await transaction.RollbackAsync(cancellationToken);
        _isCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
