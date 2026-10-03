using Outbox.DependencyInjection;
using Outbox.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Outbox.IntegrationTests.Infrastructure;

namespace Outbox.IntegrationTests.Acceptance;

internal static class TestHelpers
{
    public static ServiceProvider CreateServiceProvider(
        this IOutboxDatabaseFixture fixture,
        HandlerProbe probe)
    {
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddSingleton<ILogger<OutboxProcessor>>(NullLogger<OutboxProcessor>.Instance);
        services.AddOutbox(configuration =>
        {
            fixture.ConfigureOutbox(configuration);
            configuration.AddHandler<RecordingOutboxHandler>();
        });

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    extension(ServiceProvider serviceProvider)
    {
        public async Task AddMessageAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();

            await outbox.AddAsync(
                OutboxAcceptanceData.GetPayload(),
                OutboxAcceptanceData.GetHeaders(),
                cancellationToken);
        }

        public async Task AddScheduledMessageAsync(DateTimeOffset scheduledAt,
            CancellationToken cancellationToken = default)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();

            await outbox.AddAsync(
                OutboxAcceptanceData.GetPayload(),
                OutboxAcceptanceData.GetHeaders(),
                scheduledAt,
                cancellationToken);
        }

        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            return await processor.ProcessNextAsync(cancellationToken);
        }
    }
}
