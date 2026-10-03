using System.Data;

namespace Outbox;

public interface IOutbox
{
    Task EnsureSchema(CancellationToken cancellationToken);

    Task EnsureSchema();

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        DateTimeOffset scheduledAt);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        CancellationToken cancellationToken);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        DateTimeOffset scheduledAt);

    Task AddAsync<T>(
        T payload,
        IDictionary<string, string> headers,
        IDbTransaction transaction,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken);
}
