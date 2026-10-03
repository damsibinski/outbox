using Outbox.DependencyInjection;

namespace Outbox.Postgres.DependencyInjection;

public static class OutboxConfigurationExtensions
{
    public static OutboxConfiguration UsePostgres(
        this OutboxConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return configuration.UseRepository(
            _ => new PostgresOutboxRepository(connectionString));
    }
}
