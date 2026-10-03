using Outbox.IntegrationTests.Infrastructure;

namespace Outbox.IntegrationTests.Postgres;

[Collection(nameof(PostgresOutboxCollection))]
public sealed class PostgresOutboxRepositoryIntegrationTests(PostgresOutboxFixture fixture) : IAsyncLifetime
{
    [Fact]
    public async Task WhenEnsureSchemaIsCalledTwice_ShouldNotThrow()
    {
        await fixture.Repository.EnsureSchema(CancellationToken.None);
        await fixture.Repository.EnsureSchema(CancellationToken.None);
    }

    [Fact]
    public async Task WhenAddAsyncUsesExternalTransactionAndCommits_ShouldPersistMessage()
    {
        var payload = OutboxTestData.CreatePayload(data: "external-transaction");

        await fixture.ExecuteInCommittedTransactionAsync(
            async (_, transaction) =>
            {
                await fixture.Repository.AddAsync(payload, transaction, CancellationToken.None);
            });

        (await fixture.CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenAddAsyncUsesExternalTransactionAndRollsBack_ShouldNotPersistMessage()
    {
        var payload = OutboxTestData.CreatePayload(data: "rolled-back");

        await fixture.ExecuteInRolledBackTransactionAsync(
            async (_, transaction) =>
            {
                await fixture.Repository.AddAsync(payload, transaction, CancellationToken.None);
            });

        (await fixture.CountMessagesAsync()).ShouldBe(0);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetDatabaseAsync();
}
