using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Outbox.Postgres;

internal sealed class PostgresOutboxUnitOfWork(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction) : IOutboxUnitOfWork
{
    private bool _isCompleted;

    public Task AddAsync(Payload payload, CancellationToken cancellationToken)
    {
        return InsertAsync(connection, transaction, payload, cancellationToken);
    }

    internal static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Payload payload,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = PostgresVars.Queries.InsertOutboxMessage;

        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.Id, NpgsqlDbType.Uuid)
            {
                Value = Guid.NewGuid()
            });
        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.Data, NpgsqlDbType.Text)
            {
                Value = payload.Data
            });
        command.Parameters.Add(
            new NpgsqlParameter(
                PostgresVars.Parameters.Type,
                NpgsqlDbType.Varchar,
                PostgresVars.TypeColumnMaxLength)
            {
                Value = payload.Type
            });
        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.Headers, NpgsqlDbType.Text)
            {
                Value = JsonSerializer.Serialize(
                    payload.Headers,
                    Vars.Json.SerializerOptions)
            });
        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.InsertDate, NpgsqlDbType.TimestampTz)
            {
                Value = DateTime.UtcNow
            });
        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.ScheduledAt, NpgsqlDbType.TimestampTz)
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
        command.CommandText = PostgresVars.Queries.ClaimNextOutboxMessage;
        command.Parameters.Add(
            new NpgsqlParameter(PostgresVars.Parameters.Now, NpgsqlDbType.TimestampTz)
            {
                Value = DateTimeOffset.UtcNow
            });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OutboxMessage(
            reader.GetGuid(reader.GetOrdinal(PostgresVars.Columns.Id)),
            reader.GetString(reader.GetOrdinal(PostgresVars.Columns.Data)),
            reader.GetString(reader.GetOrdinal(PostgresVars.Columns.Type)),
            reader.GetString(reader.GetOrdinal(PostgresVars.Columns.Headers)),
            reader.GetDateTime(reader.GetOrdinal(PostgresVars.Columns.InsertDate)),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(PostgresVars.Columns.ScheduledAt)));
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
