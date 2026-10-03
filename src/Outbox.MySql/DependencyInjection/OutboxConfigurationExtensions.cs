using Outbox.DependencyInjection;

namespace Outbox.MySql.DependencyInjection;

public static class OutboxConfigurationExtensions
{
    public static OutboxConfiguration UseMySql(
        this OutboxConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return configuration.UseRepository(
            _ => new MySqlOutboxRepository(connectionString));
    }
}
