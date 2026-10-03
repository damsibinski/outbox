using Outbox.IntegrationTests.Infrastructure;
using OutboxService = global::Outbox.Outbox;

namespace Outbox.IntegrationTests.Acceptance;

public abstract class OutboxProviderAcceptanceTests<TFixture>(TFixture fixture) : IAsyncLifetime
    where TFixture : IOutboxDatabaseFixture
{
    private readonly TFixture _fixture = fixture;

    [Fact]
    public async Task WhenMessageIsAddedAndProcessed_ThenHandlerShouldReceiveItAndMessageShouldBeRemoved()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);

        await serviceProvider.AddMessageAsync();

        var processed = await serviceProvider.ProcessNextAsync();

        processed.ShouldBeTrue();
        var delivery = probe.Deliveries.ShouldHaveSingleItem();
        delivery.Payload.ShouldBe(OutboxAcceptanceData.GetPayload());
        delivery.Headers["correlationId"].ShouldBe("correlation-123");
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenMessageIsScheduledForFuture_ThenProcessorShouldLeaveItPending()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5);

        await serviceProvider.AddScheduledMessageAsync(scheduledAt);

        var processed = await serviceProvider.ProcessNextAsync();

        processed.ShouldBeFalse();
        probe.Deliveries.ShouldBeEmpty();
        (await _fixture.CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenHandlerFails_ThenMessageShouldRemainAvailableForRetry()
    {
        var probe = new HandlerProbe
        {
            Result = new Failure("acceptance.handler-failed")
        };
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);
        await serviceProvider.AddMessageAsync();

        var firstAttemptProcessed = await serviceProvider.ProcessNextAsync();

        firstAttemptProcessed.ShouldBeTrue();
        probe.Deliveries.Count.ShouldBe(1);
        (await _fixture.CountMessagesAsync()).ShouldBe(1);

        probe.Result = new Success();
        var retryProcessed = await serviceProvider.ProcessNextAsync();

        retryProcessed.ShouldBeTrue();
        probe.Deliveries.Count.ShouldBe(2);
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenCallerOwnedTransactionCommits_ThenMessageShouldBePersisted()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var sut = new OutboxService(_fixture.Repository);

        await sut.AddAsync(
            OutboxAcceptanceData.GetPayload(),
            OutboxAcceptanceData.GetHeaders(),
            transaction,
            CancellationToken.None);
        await transaction.CommitAsync();

        (await _fixture.CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenCallerOwnedTransactionRollsBack_ThenMessageShouldNotBePersisted()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var sut = new OutboxService(_fixture.Repository);

        await sut.AddAsync(
            OutboxAcceptanceData.GetPayload(),
            OutboxAcceptanceData.GetHeaders(),
            transaction,
            CancellationToken.None);
        await transaction.RollbackAsync();

        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.ResetDatabaseAsync();
}

[Collection(nameof(PostgresOutboxCollection))]
public sealed class PostgresOutboxAcceptanceTests(PostgresOutboxFixture fixture)
    : OutboxProviderAcceptanceTests<PostgresOutboxFixture>(fixture);

[Collection(nameof(SqlServerOutboxCollection))]
public sealed class SqlServerOutboxAcceptanceTests(SqlServerOutboxFixture fixture)
    : OutboxProviderAcceptanceTests<SqlServerOutboxFixture>(fixture);

[Collection(nameof(MySqlOutboxCollection))]
public sealed class MySqlOutboxAcceptanceTests(MySqlOutboxFixture fixture)
    : OutboxProviderAcceptanceTests<MySqlOutboxFixture>(fixture);
