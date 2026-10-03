using Outbox.DependencyInjection;

namespace Outbox.SqlServer.DependencyInjection;

public static class OutboxConfigurationExtensions
{
    public static OutboxConfiguration UseSqlServer(
        this OutboxConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return configuration.UseRepository(
            _ => new SqlOutboxRepository(connectionString));
    }
}
