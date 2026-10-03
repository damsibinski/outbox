<p align="center">
  <img src="assets/logo.svg" alt="Outbox logo" width="144" height="144" />
</p>

<h1 align="center">Outbox</h1>

<p align="center">
  Transactional outbox persistence and background processing for .NET.
</p>

<p align="center">
  <a href="https://github.com/damsibinski/outbox/actions/workflows/ci.yml"><img src="https://github.com/damsibinski/outbox/actions/workflows/ci.yml/badge.svg" alt="CI status" /></a>
  <a href="https://www.nuget.org/packages/Outbox.Core"><img src="https://img.shields.io/nuget/v/Outbox.Core.svg" alt="NuGet version" /></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License" /></a>
</p>

Outbox stores messages in the same database transaction as your application
changes, then delivers them through a small hosted worker. Provider-specific
repositories use row-level claiming so multiple application instances can
process messages without taking the same row. The API stays intentionally
small: register a provider and handler, ensure the schema, and enqueue through
`IOutbox`.

## Contents

- [Packages](#packages)
- [Quick start](#quick-start)
- [Transactions](#transactions)
- [Scheduled messages](#scheduled-messages)
- [Processing model](#processing-model)
- [Failures and retries](#failures-and-retries)
- [Database providers](#database-providers)
- [Payload compatibility](#payload-compatibility)
- [Public API](#public-api)
- [Testing](#testing)
- [Build requirements](#build-requirements)
- [Contributing](#contributing)
- [License](#license)

## Packages

| Package | Purpose |
| ------- | ------- |
| [`Outbox.Core`](https://www.nuget.org/packages/Outbox.Core) | Core abstractions, `AddOutbox`, JSON serialization, and the hosted processor. |
| [`Outbox.SqlServer`](https://www.nuget.org/packages/Outbox.SqlServer) | SQL Server storage and `UseSqlServer`. |
| [`Outbox.Postgres`](https://www.nuget.org/packages/Outbox.Postgres) | PostgreSQL storage and `UsePostgres`. |
| [`Outbox.MySql`](https://www.nuget.org/packages/Outbox.MySql) | MySQL storage and `UseMySql`. |

Install exactly one database provider. It brings `Outbox.Core` transitively and
creates an `OutboxMessages` table in the configured database.

## Quick start

All examples target .NET 10 and use SQL Server. PostgreSQL and MySQL share the
same core API.

### 1. Install a provider

```bash
dotnet add package Outbox.SqlServer
```

Use `Outbox.Postgres` or `Outbox.MySql` for the other supported databases.
The provider package installs `Outbox.Core` automatically.

### 2. Define a message and handler

```csharp
public sealed record OrderPlaced(Guid OrderId);

public sealed class OrderPlacedHandler : IOutboxHandler
{
    public Task<Result> HandleAsync<T>(
        T payload,
        IDictionary<string, string> headers)
    {
        if (payload is not OrderPlaced message)
            return Task.FromResult<Result>(
                new Failure("message.unsupported"));

        // Perform an idempotent side effect for message.OrderId.
        return Task.FromResult<Result>(new Success());
    }
}
```

One `IOutboxHandler` registration receives every payload type. Pattern-match
the payload or delegate to application-specific services.

### 3. Register Outbox

```csharp
using Outbox.DependencyInjection;
using Outbox.SqlServer.DependencyInjection;

var connectionString =
    builder.Configuration.GetConnectionString("ApplicationDatabase")
    ?? throw new InvalidOperationException(
        "ApplicationDatabase is missing.");

builder.Services.AddOutbox(options =>
{
    options
        .UseSqlServer(connectionString)
        .AddHandler<OrderPlacedHandler>();
});
```

`AddOutbox` registers the core services and starts a hosted processor. Always
register an application handler. Without `AddHandler`, the fallback handler
logs and acknowledges every message.

### 4. Create the schema

Creates the required table and indexes.

```csharp
await using (var scope = app.Services.CreateAsyncScope())
{
    var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
    await outbox.EnsureSchema(CancellationToken.None);
}
```

To deploy schema without calling `EnsureSchema` in code, run the SQL script for
your provider:

| Provider | Script |
| -------- | ------ |
| SQL Server | [src/Outbox.SqlServer/OutboxMessages.sql](src/Outbox.SqlServer/OutboxMessages.sql) |
| PostgreSQL | [src/Outbox.Postgres/OutboxMessages.sql](src/Outbox.Postgres/OutboxMessages.sql) |
| MySQL | [src/Outbox.MySql/OutboxMessages.sql](src/Outbox.MySql/OutboxMessages.sql) |

### 5. Enqueue a message

```csharp
app.MapPost("/orders/{orderId:guid}/notify", async (
    Guid orderId,
    IOutbox outbox,
    CancellationToken cancellationToken) =>
{
    await outbox.AddAsync(
        new OrderPlaced(orderId),
        new Dictionary<string, string>
        {
            ["correlationId"] = Guid.NewGuid().ToString("N")
        },
        cancellationToken);

    return Results.Accepted();
});
```

This overload commits the message in an Outbox-owned transaction. To commit a
business write and its message atomically, pass the application's transaction.

## Transactions

The transactional outbox guarantee comes from writing business data and the
outbox row through the same database connection and transaction.

```csharp
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync(cancellationToken);

await using var transaction =
    (SqlTransaction)await connection.BeginTransactionAsync(
        cancellationToken);

try
{
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
        "UPDATE Orders SET Status = 'Placed' WHERE Id = @id";
    command.Parameters.AddWithValue("@id", orderId);
    await command.ExecuteNonQueryAsync(cancellationToken);

    await outbox.AddAsync(
        new OrderPlaced(orderId),
        new Dictionary<string, string>(),
        transaction,
        cancellationToken);

    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(CancellationToken.None);
    throw;
}
```

If the transaction commits, both writes become visible. If it rolls back,
neither write is persisted.

When passing an `IDbTransaction`:

- the caller owns the connection, commit, rollback, and disposal;
- Outbox only inserts its row;
- the transaction must be `SqlTransaction`, `NpgsqlTransaction`, or
  `MySqlTransaction`, matching the configured provider;
- the transaction must target the database containing `OutboxMessages`.

Ambient `TransactionScope` integration is not part of the current API.

## Scheduled messages

Pass a UTC `DateTimeOffset` to delay eligibility:

```csharp
await outbox.AddAsync(
    payload,
    headers,
    DateTimeOffset.UtcNow.AddMinutes(10),
    cancellationToken);
```

Scheduling guarantees "not before", not an exact execution time. Worker
availability, database load, and earlier messages can delay handling.

The same option is available with a caller-owned transaction:

```csharp
await outbox.AddAsync(
    payload,
    headers,
    transaction,
    scheduledAt,
    cancellationToken);
```

## Processing model

`AddOutbox` runs a background worker that processes due messages. For each
message it claims the earliest eligible row, calls your `IOutboxHandler`, and
removes the row when the handler returns `Success`. Any other outcome leaves the
row in the table for a later attempt.

When nothing is due, the worker waits briefly before checking again. After an
unexpected error it waits a few seconds before the next check.

### Delivery guarantee

Delivery is **at least once**, not exactly once. A handler can complete an
external side effect and then lose the database commit, making the same message
available again.

Handlers must be idempotent. Use a stable business identifier from the payload
or headers as an idempotency key.

### Concurrency and ordering

All providers use row-level claiming that skips locked rows. Multiple
application instances can therefore process different messages concurrently
without claiming the same row at the same time.

With one worker, eligible messages are selected by `ScheduledAt` and then
`InsertDate`. Across multiple workers, completion order is not guaranteed.
Outbox does not provide aggregate or partition ordering.

## Failures and retries

Handlers decide what happens to the current attempt by returning `Success` or
`Failure`. The message stays in the table until a handler returns `Success`.

```csharp
public sealed class OrderPlacedHandler(IPaymentGateway gateway) : IOutboxHandler
{
    public async Task<Result> HandleAsync<T>(
        T payload,
        IDictionary<string, string> headers)
    {
        if (payload is not OrderPlaced order)
            return new Failure("order.unsupported-type");

        if (await gateway.WasAlreadyProcessedAsync(order.OrderId))
            return new Success();

        try
        {
            await gateway.ChargeAsync(order.OrderId);
            return new Success();
        }
        catch (NotRetryableException ex)
        {
            await gateway.RecordTerminalFailureAsync(order.OrderId, ex);
            return new Success();
        }
    }
}
```

**Success** — the row is removed and the message will not be delivered again.

```csharp
return new Success();
```

Use this when the side effect completed, when duplicate delivery is safe
because the work was already done (idempotent handler), or when the failure must
not be retried and you handled it elsewhere (audit log, manual queue, alert).

```csharp
catch (NotRetryableException ex)
{
    await RecordTerminalFailureAsync(order.OrderId, ex);
    return new Success();
}
```

**Transient failure** — the row stays; Outbox waits about five seconds before
trying again.

```csharp
return new Failure("payments.timeout", isTransient: true);
```

**Permanent failure** — the row stays and can be picked up again on the next
poll (no extra delay). Use when you want Outbox to keep trying until you fix
the handler or the data.

```csharp
return new Failure("payments.declined");
```

**Unhandled exception** — same as a transient failure from the caller’s
perspective: the row remains and Outbox retries after a short delay.

```csharp
await gateway.ChargeAsync(order.OrderId); // exception → row kept, retry later
return new Success();
```

**Cancellation** — when the application stops while a message is in flight,
processing aborts and the row remains for the next run.

`IsPermanent` and `IsTransient` describe the failure; they do not remove or
quarantine the message by themselves.

> [!WARNING]
> A permanently failing message can be selected repeatedly without delay and
> prevent a single worker from reaching later rows. The current library does
> not provide retry counts, configurable backoff, poison-message quarantine, or
> a dead-letter table.

## Database providers

Register exactly one provider.

| Provider | Registration | Driver |
| -------- | ------------ | ------ |
| SQL Server | `UseSqlServer(connectionString)` | `Microsoft.Data.SqlClient` |
| PostgreSQL | `UsePostgres(connectionString)` | `Npgsql` |
| MySQL | `UseMySql(connectionString)` | `MySqlConnector` |

### SQL Server

```csharp
using Outbox.SqlServer.DependencyInjection;

services.AddOutbox(options =>
{
    options
        .UseSqlServer(connectionString)
        .AddHandler<ApplicationOutboxHandler>();
});
```

The provider creates `dbo.OutboxMessages`.

### PostgreSQL

```csharp
using Outbox.Postgres.DependencyInjection;

services.AddOutbox(options =>
{
    options
        .UsePostgres(connectionString)
        .AddHandler<ApplicationOutboxHandler>();
});
```

The provider creates the quoted table `"OutboxMessages"` in the connection's
current schema.

### MySQL

```csharp
using Outbox.MySql.DependencyInjection;

services.AddOutbox(options =>
{
    options
        .UseMySql(connectionString)
        .AddHandler<ApplicationOutboxHandler>();
});
```

The provider requires MySQL 8-compatible `SKIP LOCKED` behavior and creates an
InnoDB table using `utf8mb4_0900_ai_ci`.

## Payload compatibility

Outbox serializes messages and headers with `System.Text.Json` using:

- camel-case property names;
- case-insensitive deserialization;
- string enum values.

The assembly-qualified CLR type name is persisted with each row. Renaming a
message type, moving it to another assembly, or introducing an incompatible
JSON change can stop old rows from deserializing.

Treat messages as durable contracts:

- prefer immutable DTOs with stable names and namespaces;
- make schema changes backward compatible;
- keep enum names stable;
- drain or migrate pending rows before changing type identity.

## Public API

Most applications only need these groups:

| API | Purpose |
| --- | ------- |
| `AddOutbox` | Registers core services and the hosted processor. |
| `UseSqlServer`, `UsePostgres`, `UseMySql` | Selects one database repository. |
| `AddHandler<THandler>` | Registers one scoped application handler. |
| `IOutbox.EnsureSchema` | Idempotently creates the table and index. |
| `IOutbox.AddAsync` | Enqueues immediately, later, or in a caller-owned transaction. |
| `IOutboxHandler.HandleAsync` | Handles all deserialized payload types. |
| `Success`, `Failure` | Completes or retains the claimed message. |

### `IOutbox.AddAsync`

The overloads combine these options:

| Parameter | Meaning |
| --------- | ------- |
| `payload` | Typed object serialized as JSON. |
| `headers` | String key/value metadata stored with the payload. |
| `scheduledAt` | Earliest time at which the row is eligible. |
| `transaction` | Optional caller-owned provider transaction. |
| `cancellationToken` | Optional cancellation for database I/O. |

Without `scheduledAt`, the message is immediately eligible. Without a
transaction, Outbox creates and commits its own unit of work.

## Testing

Run unit tests:

```bash
dotnet test tests/Outbox.Tests/Outbox.Tests.csproj
```

Provider integration tests use Testcontainers and require Docker:

```bash
dotnet test tests/Outbox.IntegrationTests/Outbox.IntegrationTests.csproj
```

The same acceptance scenarios run against SQL Server, PostgreSQL, and MySQL.

## Build requirements

All projects target .NET 10. Building the repository requires the .NET 10 SDK.
Docker is required only for integration tests. The library has not declared
trimming or Native AOT compatibility.

Build and run the complete test suite:

```bash
dotnet build Outbox.slnx -c Release
dotnet test Outbox.slnx -c Release
```

See [releases](https://github.com/damsibinski/outbox/releases) for published
versions.

## Contributing

Contributions are welcome — bug fixes, tests, docs, and provider improvements.
See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, tests, and how to open a pull
request.

## License

Outbox is licensed under the [MIT License](LICENSE).
