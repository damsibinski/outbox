using Outbox.IntegrationTests.Infrastructure;

namespace Outbox.IntegrationTests.MySql;

[Collection(nameof(MySqlOutboxCollection))]
public sealed class MySqlOutboxUnitOfWorkIntegrationTests(MySqlOutboxFixture fixture) : IAsyncLifetime
{
    [Fact]
    public async Task WhenMessageIsAddedAndCommitted_ShouldPersistInDatabase()
    {
        var payload = OutboxTestData.CreatePayload();

        await using (var unitOfWork = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            await unitOfWork.AddAsync(payload, CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
        }

        (await fixture.CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenTransactionIsRolledBack_MessageShouldNotPersist()
    {
        var payload = OutboxTestData.CreatePayload();

        await using (var unitOfWork = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            await unitOfWork.AddAsync(payload, CancellationToken.None);
            await unitOfWork.RollbackAsync(CancellationToken.None);
        }

        (await fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenClaimNextIsCalled_ShouldReturnEarliestReadyMessageInOrder()
    {
        var first = OutboxTestData.CreatePayload(
            data: "first",
            scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        var second = OutboxTestData.CreatePayload(
            data: "second",
            scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        await using (var insert = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            await insert.AddAsync(first, CancellationToken.None);
            await insert.AddAsync(second, CancellationToken.None);
            await insert.CommitAsync(CancellationToken.None);
        }

        await using var claim = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None);

        var firstClaim = await claim.ClaimNextAsync(CancellationToken.None);
        firstClaim.ShouldNotBeNull();
        firstClaim.Data.ShouldBe("first");

        var secondClaim = await claim.ClaimNextAsync(CancellationToken.None);
        secondClaim.ShouldNotBeNull();
        secondClaim.Data.ShouldBe("second");

        (await claim.ClaimNextAsync(CancellationToken.None)).ShouldBeNull();
        await claim.CommitAsync(CancellationToken.None);

        (await fixture.CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WhenScheduledAtIsInFuture_ClaimNextShouldReturnNull()
    {
        var payload = OutboxTestData.CreatePayload(
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));

        await using (var insert = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            await insert.AddAsync(payload, CancellationToken.None);
            await insert.CommitAsync(CancellationToken.None);
        }

        await using var claim = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None);
        (await claim.ClaimNextAsync(CancellationToken.None)).ShouldBeNull();
        await claim.RollbackAsync(CancellationToken.None);

        (await fixture.CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenClaimIsRolledBack_MessageShouldRemainForNextClaim()
    {
        var payload = OutboxTestData.CreatePayload(data: "claim-rollback");

        await using (var insert = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            await insert.AddAsync(payload, CancellationToken.None);
            await insert.CommitAsync(CancellationToken.None);
        }

        await using (var failedClaim = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None))
        {
            var claimed = await failedClaim.ClaimNextAsync(CancellationToken.None);
            claimed.ShouldNotBeNull();
            claimed.Data.ShouldBe("claim-rollback");
            await failedClaim.RollbackAsync(CancellationToken.None);
        }

        (await fixture.CountMessagesAsync()).ShouldBe(1);

        await using var successfulClaim = await fixture.Repository.BeginUnitOfWorkAsync(CancellationToken.None);
        var reclaimed = await successfulClaim.ClaimNextAsync(CancellationToken.None);
        reclaimed.ShouldNotBeNull();
        reclaimed.Data.ShouldBe("claim-rollback");
        await successfulClaim.CommitAsync(CancellationToken.None);

        (await fixture.CountMessagesAsync()).ShouldBe(0);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetDatabaseAsync();
}
