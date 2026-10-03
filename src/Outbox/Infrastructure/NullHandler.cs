using Microsoft.Extensions.Logging;

namespace Outbox.Infrastructure;

public sealed class NullHandler(ILogger<NullHandler> logger) : IOutboxHandler
{
    public Task<Result> HandleAsync<T>(T payload, IDictionary<string, string> headers)
    {
        logger.LogInformation(
            "Outbox message received. PayloadType: {PayloadType}, Headers: {@Headers}",
            typeof(T).FullName,
            headers);

        return Task.FromResult<Result>(new Success());
    }
}
