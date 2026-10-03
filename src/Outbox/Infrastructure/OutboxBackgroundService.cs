using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Outbox.Infrastructure;

public sealed class OutboxBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            var processed = await processor.ProcessNextAsync(stoppingToken);

            if (!processed)
                await Task.Delay(Vars.Processor.PollInterval, stoppingToken);
        }
    }
}
