using System.Data;
using System.Data.Common;
using MySqlConnector;
using Outbox.DependencyInjection;
using Outbox.MySql;
using Outbox.MySql.DependencyInjection;
using Respawn;
using Testcontainers.MySql;

namespace Outbox.IntegrationTests.Infrastructure;

public sealed class MySqlOutboxFixture : IAsyncLifetime, IOutboxDatabaseFixture
{
    private readonly MySqlContainer _container =
        new MySqlBuilder(TestcontainerImages.MySql).Build();
    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public IOutboxRepository Repository { get; private set; } = null!;

    public void ConfigureOutbox(OutboxConfiguration configuration)
    {
        configuration.UseMySql(ConnectionString);
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Repository = new MySqlOutboxRepository(ConnectionString);
        await Repository.EnsureSchema(CancellationToken.None);

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.MySql
            });
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await _respawner.ResetAsync(connection);
    }

    public async Task<int> CountMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM `OutboxMessages`;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task ExecuteInCommittedTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await action(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ExecuteInRolledBackTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await action(connection, transaction);
        await transaction.RollbackAsync(cancellationToken);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(nameof(MySqlOutboxCollection))]
public sealed class MySqlOutboxCollectionDefinition : ICollectionFixture<MySqlOutboxFixture>;

public sealed class MySqlOutboxCollection;
