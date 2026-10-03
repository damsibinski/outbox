namespace Outbox.Infrastructure;

public interface IOutboxHandlerInvoker
{
    Task<Result> InvokeAsync(OutboxMessage message, CancellationToken cancellationToken);
}
