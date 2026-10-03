using Microsoft.Extensions.DependencyInjection;
using Outbox.IntegrationTests.Infrastructure;
using OutboxService = global::Outbox.Outbox;

namespace Outbox.IntegrationTests.Acceptance;

public abstract class OutboxProviderAcceptanceAdditionalPathsTests<TFixture>(TFixture fixture) : IAsyncLifetime
    where TFixture : IOutboxDatabaseFixture
{
    private readonly TFixture _fixture = fixture;

    [Fact]
    public async Task WhenOutboxQueueIsEmpty_ThenProcessNextShouldReturnFalse()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);

        var processed = await serviceProvider.ProcessNextAsync();

        processed.ShouldBeFalse();
        probe.Deliveries.ShouldBeEmpty();
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenTwoMessagesAreAdded_ThenProcessorShouldProcessInScheduledOrder()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await outbox.AddAsync(
                new AcceptancePayload("first", AcceptanceStatus.Enabled),
                OutboxAcceptanceData.GetHeaders(),
                DateTimeOffset.UtcNow.AddMinutes(-10),
                CancellationToken.None);
            await outbox.AddAsync(
                new AcceptancePayload("second", AcceptanceStatus.Enabled),
                OutboxAcceptanceData.GetHeaders(),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                CancellationToken.None);
        }

        (await serviceProvider.ProcessNextAsync()).ShouldBeTrue();
        probe.Deliveries[^1].Payload.Name.ShouldBe("first");

        (await serviceProvider.ProcessNextAsync()).ShouldBeTrue();
        probe.Deliveries[^1].Payload.Name.ShouldBe("second");

        (await serviceProvider.ProcessNextAsync()).ShouldBeFalse();
        probe.Deliveries.Count.ShouldBe(2);
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenMessageIsPublishedInsideCommittedTransaction_ThenProcessorShouldDeliverAfterCommit()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);

        await using (var connection = await _fixture.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var outbox = new OutboxService(_fixture.Repository);
            await outbox.AddAsync(
                OutboxAcceptanceData.GetPayload(),
                OutboxAcceptanceData.GetHeaders(),
                transaction,
                CancellationToken.None);
            await transaction.CommitAsync();
        }

        (await serviceProvider.ProcessNextAsync()).ShouldBeTrue();
        probe.Deliveries.ShouldHaveSingleItem();
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenEnsureSchemaIsCalledThroughDi_ThenAddAndProcessShouldSucceed()
    {
        var probe = new HandlerProbe();
        await using var serviceProvider = _fixture.CreateServiceProvider(probe);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await outbox.EnsureSchema(CancellationToken.None);
            await outbox.EnsureSchema(CancellationToken.None);
        }

        await serviceProvider.AddMessageAsync();
        (await serviceProvider.ProcessNextAsync()).ShouldBeTrue();
        probe.Deliveries.ShouldHaveSingleItem();
        (await _fixture.CountMessagesAsync()).ShouldBe(0);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _fixture.ResetDatabaseAsync();
}

[Collection(nameof(PostgresOutboxCollection))]
public sealed class PostgresOutboxAcceptanceAdditionalPathsTests(PostgresOutboxFixture fixture)
    : OutboxProviderAcceptanceAdditionalPathsTests<PostgresOutboxFixture>(fixture);

[Collection(nameof(SqlServerOutboxCollection))]
public sealed class SqlServerOutboxAcceptanceAdditionalPathsTests(SqlServerOutboxFixture fixture)
    : OutboxProviderAcceptanceAdditionalPathsTests<SqlServerOutboxFixture>(fixture);

[Collection(nameof(MySqlOutboxCollection))]
public sealed class MySqlOutboxAcceptanceAdditionalPathsTests(MySqlOutboxFixture fixture)
    : OutboxProviderAcceptanceAdditionalPathsTests<MySqlOutboxFixture>(fixture);
