using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Outbox.Infrastructure;

public sealed class OutboxHandlerInvoker(IServiceProvider serviceProvider) : IOutboxHandlerInvoker
{
    public async Task<Result> InvokeAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payloadType = Type.GetType(message.Type, throwOnError: false);
        if (payloadType is null)
            return new Failure(Vars.Errors.PayloadTypeNotFound);

        var handler = serviceProvider.GetService<IOutboxHandler>();
        if (handler is null)
            return new Failure(Vars.Errors.HandlerNotRegistered);

        var payload = JsonSerializer.Deserialize(message.Data, payloadType, Vars.Json.SerializerOptions);
        if (payload is null)
            return new Failure(Vars.Errors.PayloadDeserializationFailed);

        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers, Vars.Json.SerializerOptions)
            ?? [];

        var handleMethod = typeof(IOutboxHandler)
            .GetMethod(nameof(IOutboxHandler.HandleAsync))
            ?.MakeGenericMethod(payloadType);
        if (handleMethod is null)
            return new Failure(Vars.Errors.HandlerNotRegistered);

        var invokeResult = handleMethod.Invoke(handler, [payload, headers]);
        if (invokeResult is not Task<Result> handleTask)
            return new Failure(Vars.Errors.HandlerNotRegistered);

        return await handleTask.WaitAsync(cancellationToken);
    }
}
