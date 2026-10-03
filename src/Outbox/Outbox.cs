using System.Data;
using System.Text.Json;

namespace Outbox;

public sealed class Outbox(IOutboxRepository repository) : IOutbox
{
    public Task EnsureSchema(CancellationToken cancellationToken)
    {
        return repository.EnsureSchema(cancellationToken);
    }

    public Task EnsureSchema()
    {
        return repository.EnsureSchema();
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers)
    {
        return AddAsync(payload, headers, DateTimeOffset.MinValue, CancellationToken.None);
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        return AddAsync(payload, headers, DateTimeOffset.MinValue, cancellationToken);
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        DateTimeOffset scheduledAt)
    {
        return AddAsync(payload, headers, scheduledAt, CancellationToken.None);
    }

    public async Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        var payloadData = CreatePayload(payload, headers, scheduledAt);

        await using var unitOfWork = await repository.BeginUnitOfWorkAsync(cancellationToken);
        await unitOfWork.AddAsync(payloadData, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction)
    {
        return AddAsync(
            payload,
            headers,
            transaction,
            DateTimeOffset.MinValue,
            CancellationToken.None);
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        return AddAsync(
            payload,
            headers,
            transaction,
            DateTimeOffset.MinValue,
            cancellationToken);
    }

    public Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        DateTimeOffset scheduledAt)
    {
        return AddAsync(
            payload,
            headers,
            transaction,
            scheduledAt,
            CancellationToken.None);
    }

    public async Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await repository.AddAsync(
            CreatePayload(payload, headers, scheduledAt),
            transaction,
            cancellationToken);
    }

    private static Payload CreatePayload<T>(
        T payload,
        IDictionary<string, string> headers,
        DateTimeOffset scheduledAt)
    {
        var serialize = JsonSerializer.Serialize(payload, Vars.Json.SerializerOptions)
                        ?? throw new InvalidOperationException(string.Format(Vars.Errors.SerializePayloadFailedMessage,
                            typeof(T).Name));
        var assemblyQualifiedName = typeof(T).AssemblyQualifiedName
                                    ?? throw new InvalidOperationException(
                                        string.Format(Vars.Errors.AssemblyQualifiedNameMissingMessage,
                                            typeof(T).FullName));

        return new Payload(serialize, assemblyQualifiedName, headers, scheduledAt);
    }
}
