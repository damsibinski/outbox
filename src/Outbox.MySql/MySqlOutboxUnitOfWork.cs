using System.Text.Json;
using MySqlConnector;

namespace Outbox.MySql;

internal sealed class MySqlOutboxUnitOfWork(
    MySqlConnection connection,
    MySqlTransaction transaction) : IOutboxUnitOfWork
{
    private bool _isCompleted;

    public Task AddAsync(Payload payload, CancellationToken cancellationToken)
    {
        return InsertAsync(connection, transaction, payload, cancellationToken);
    }

    internal static async Task InsertAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        Payload payload,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = MySqlVars.Queries.InsertOutboxMessage;

        command.Parameters.Add(
            new MySqlParameter(
                MySqlVars.Parameters.Id,
                MySqlDbType.VarChar,
                MySqlVars.IdColumnMaxLength)
            {
                Value = Guid.NewGuid().ToString("D")
            });
        command.Parameters.Add(
            new MySqlParameter(MySqlVars.Parameters.Data, MySqlDbType.LongText)
            {
                Value = payload.Data
            });
        command.Parameters.Add(
            new MySqlParameter(
                MySqlVars.Parameters.Type,
                MySqlDbType.VarChar,
                MySqlVars.TypeColumnMaxLength)
            {
                Value = payload.Type
            });
        command.Parameters.Add(
            new MySqlParameter(MySqlVars.Parameters.Headers, MySqlDbType.LongText)
            {
                Value = JsonSerializer.Serialize(
                    payload.Headers,
                    Vars.Json.SerializerOptions)
            });
        command.Parameters.Add(
            new MySqlParameter(MySqlVars.Parameters.InsertDate, MySqlDbType.DateTime)
            {
                Value = DateTime.UtcNow
            });
        command.Parameters.Add(
            new MySqlParameter(MySqlVars.Parameters.ScheduledAt, MySqlDbType.DateTime)
            {
                Value = ToMySqlDateTime(payload.ScheduledAt)
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OutboxMessage?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        OutboxMessage? message;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = MySqlVars.Queries.SelectNextOutboxMessage;
            command.Parameters.Add(
                new MySqlParameter(MySqlVars.Parameters.Now, MySqlDbType.DateTime)
                {
                    Value = DateTime.UtcNow
                });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            message = new OutboxMessage(
                ReadGuid(reader, MySqlVars.Columns.Id),
                reader.GetString(reader.GetOrdinal(MySqlVars.Columns.Data)),
                reader.GetString(reader.GetOrdinal(MySqlVars.Columns.Type)),
                reader.GetString(reader.GetOrdinal(MySqlVars.Columns.Headers)),
                GetUtcDateTime(reader, MySqlVars.Columns.InsertDate),
                new DateTimeOffset(GetUtcDateTime(reader, MySqlVars.Columns.ScheduledAt)));
        }

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = MySqlVars.Queries.DeleteOutboxMessage;
        deleteCommand.Parameters.Add(
            new MySqlParameter(
                MySqlVars.Parameters.Id,
                MySqlDbType.VarChar,
                MySqlVars.IdColumnMaxLength)
            {
                Value = message.Id.ToString("D")
            });

        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        return message;
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

    private static Guid ReadGuid(MySqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetFieldType(ordinal) == typeof(Guid)
            ? reader.GetGuid(ordinal)
            : Guid.Parse(reader.GetString(ordinal));
    }

    private static DateTime GetUtcDateTime(
        MySqlDataReader reader,
        string columnName)
    {
        var value = reader.GetDateTime(reader.GetOrdinal(columnName));
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime ToMySqlDateTime(DateTimeOffset value)
    {
        var utcDateTime = value.UtcDateTime;
        return utcDateTime < MySqlVars.MinimumDateTime
            ? MySqlVars.MinimumDateTime
            : utcDateTime;
    }
}
