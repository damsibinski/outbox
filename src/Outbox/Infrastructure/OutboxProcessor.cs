using Microsoft.Extensions.Logging;

namespace Outbox.Infrastructure;

public sealed class OutboxProcessor(
    IOutboxRepository repository,
    IOutboxHandlerInvoker handlerInvoker,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        OutboxMessage? message = null;

        try
        {
            await using var unitOfWork = await repository.BeginUnitOfWorkAsync(cancellationToken);
            message = await unitOfWork.ClaimNextAsync(cancellationToken);
            if (message is null)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return false;
            }

            var result = await handlerInvoker.InvokeAsync(message, cancellationToken);

            if (result.IsSuccess)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                logger.LogDebug(
                    "Outbox message processed. Id: {Id}; Type: {Type}",
                    message.Id,
                    message.Type);
                return true;
            }

            await unitOfWork.RollbackAsync(CancellationToken.None);

            if (result is Failure failure)
            {
                logger.LogWarning(
                    "Outbox message processing failed. Id: {Id}; Type: {Type}; ErrorCode: {ErrorCode}; IsTransient: {IsTransient}",
                    message.Id,
                    message.Type,
                    failure.ErrorCode,
                    failure.IsTransient);

                if (failure.IsTransient)
                    await Task.Delay(Vars.Processor.TransientFailureDelay, cancellationToken);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Outbox message processing threw an exception. Id: {Id}; Type: {Type}",
                message?.Id,
                message?.Type);

            await Task.Delay(Vars.Processor.TransientFailureDelay, cancellationToken);
            return true;
        }
    }
}
