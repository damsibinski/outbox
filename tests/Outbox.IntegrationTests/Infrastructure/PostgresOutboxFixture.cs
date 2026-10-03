using System.Data;
using System.Data.Common;
using Npgsql;
using Outbox.DependencyInjection;
using Outbox.Postgres;
using Outbox.Postgres.DependencyInjection;
using Respawn;
using Testcontainers.PostgreSql;

namespace Outbox.IntegrationTests.Infrastructure;

public sealed class PostgresOutboxFixture : IAsyncLifetime, IOutboxDatabaseFixture
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder(TestcontainerImages.Postgres).Build();
    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public IOutboxRepository Repository { get; private set; } = null!;

    public void ConfigureOutbox(OutboxConfiguration configuration)
    {
        configuration.UsePostgres(ConnectionString);
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Repository = new PostgresOutboxRepository(ConnectionString);
        await Repository.EnsureSchema(CancellationToken.None);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"]
            });
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await _respawner.ResetAsync(connection);
    }

    public async Task<int> CountMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "OutboxMessages";""";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task ExecuteInCommittedTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await action(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ExecuteInRolledBackTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await action(connection, transaction);
        await transaction.RollbackAsync(cancellationToken);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresOutboxCollection))]
public sealed class PostgresOutboxCollectionDefinition : ICollectionFixture<PostgresOutboxFixture>;

public sealed class PostgresOutboxCollection;
