using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Outbox.Infrastructure;

namespace Outbox.DependencyInjection;

public sealed class OutboxConfiguration
{
    private readonly IServiceCollection _services;

    public OutboxConfiguration(IServiceCollection services)
    {
        _services = services;
        _services.TryAddScoped<IOutboxHandler, NullHandler>();
    }

    public OutboxConfiguration UseRepository(
        Func<IServiceProvider, IOutboxRepository> repositoryFactory)
    {
        ArgumentNullException.ThrowIfNull(repositoryFactory);

        _services.TryAddScoped(repositoryFactory);
        return this;
    }

    public OutboxConfiguration AddHandler<THandler>()
        where THandler : class, IOutboxHandler
    {
        _services.RemoveAll<IOutboxHandler>();
        _services.AddScoped<IOutboxHandler, THandler>();
        return this;
    }
}
