using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Outbox.Infrastructure;

namespace Outbox.DependencyInjection;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        Action<OutboxConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddScoped<IOutboxHandlerInvoker, OutboxHandlerInvoker>();
        services.TryAddScoped<IOutboxProcessor, OutboxProcessor>();
        services.TryAddScoped<IOutbox, Outbox>();

        if (services.All(descriptor => descriptor.ImplementationType != typeof(OutboxBackgroundService)))
            services.AddHostedService<OutboxBackgroundService>();

        configure(new OutboxConfiguration(services));
        return services;
    }
}
