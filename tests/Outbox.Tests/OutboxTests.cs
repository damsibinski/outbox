using System.Data;
using NSubstitute;
using OutboxService = global::Outbox.Outbox;

namespace Outbox.Tests;

public sealed class OutboxTests
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxUnitOfWork _unitOfWork;
    private readonly OutboxService _sut;

    public OutboxTests()
    {
        _repository = Substitute.For<IOutboxRepository>();
        _unitOfWork = Substitute.For<IOutboxUnitOfWork>();
        _repository
            .BeginUnitOfWorkAsync(Arg.Any<CancellationToken>())
            .Returns(_unitOfWork);

        _sut = new OutboxService(_repository);
    }

    [Fact]
    public async Task WhenEnsuringSchema_ShouldDelegateToRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        await _sut.EnsureSchema(cancellationToken);

        await _repository.Received(1).EnsureSchema(cancellationToken);
    }

    [Fact]
    public async Task WhenAddingMessageWithoutSchedule_ShouldPersistSerializedPayloadAndCommit()
    {
        var sutArg = GetTestPayload();
        var headers = GetHeaders();

        await _sut.AddAsync(sutArg, headers);

        await _repository.Received(1).BeginUnitOfWorkAsync(CancellationToken.None);
        await _unitOfWork.Received(1).AddAsync(
            Arg.Is<Payload>(payload =>
                payload.Data == """{"name":"feature-a","status":"Enabled"}""" &&
                payload.Type == typeof(TestPayload).AssemblyQualifiedName &&
                payload.Headers == headers &&
                payload.ScheduledAt == DateTimeOffset.MinValue),
            CancellationToken.None);
        await _unitOfWork.Received(1).CommitAsync(CancellationToken.None);
        await _unitOfWork.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task WhenAddingScheduledMessage_ShouldPropagateScheduleAndCancellationToken()
    {
        var sutArg = GetTestPayload();
        var scheduledAt = new DateTimeOffset(2026, 9, 18, 7, 30, 0, TimeSpan.Zero);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        await _sut.AddAsync(sutArg, GetHeaders(), scheduledAt, cancellationToken);

        await _repository.Received(1).BeginUnitOfWorkAsync(cancellationToken);
        await _unitOfWork.Received(1).AddAsync(
            Arg.Is<Payload>(payload => payload.ScheduledAt == scheduledAt),
            cancellationToken);
        await _unitOfWork.Received(1).CommitAsync(cancellationToken);
    }

    [Fact]
    public async Task WhenAddingMessageWithinTransaction_ShouldUseRepositoryWithoutStartingUnitOfWork()
    {
        var sutArg = GetTestPayload();
        var transaction = Substitute.For<IDbTransaction>();
        var scheduledAt = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        await _sut.AddAsync(sutArg, GetHeaders(), transaction, scheduledAt, cancellationToken);

        await _repository.Received(1).AddAsync(
            Arg.Is<Payload>(payload =>
                payload.Data == """{"name":"feature-a","status":"Enabled"}""" &&
                payload.Type == typeof(TestPayload).AssemblyQualifiedName &&
                payload.ScheduledAt == scheduledAt),
            transaction,
            cancellationToken);
        await _repository.DidNotReceive().BeginUnitOfWorkAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenTransactionIsNull_ShouldThrowArgumentNullException()
    {
        var sutArg = GetTestPayload();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.AddAsync(
                sutArg,
                GetHeaders(),
                null!,
                DateTimeOffset.MinValue,
                CancellationToken.None));

        exception.ParamName.ShouldBe("transaction");
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<Payload>(),
            Arg.Any<IDbTransaction>(),
            Arg.Any<CancellationToken>());
    }

    private static TestPayload GetTestPayload() => new("feature-a", TestStatus.Enabled);

    private static Dictionary<string, string> GetHeaders() => new()
    {
        ["correlationId"] = "correlation-123"
    };

    private sealed record TestPayload(string Name, TestStatus Status);

    private enum TestStatus
    {
        Disabled,
        Enabled
    }
}
