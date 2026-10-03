using global::Outbox.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Outbox.Tests.Infrastructure;

public sealed class OutboxProcessorTests
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxUnitOfWork _unitOfWork;
    private readonly IOutboxHandlerInvoker _handlerInvoker;
    private readonly OutboxProcessor _sut;

    public OutboxProcessorTests()
    {
        _repository = Substitute.For<IOutboxRepository>();
        _unitOfWork = Substitute.For<IOutboxUnitOfWork>();
        _handlerInvoker = Substitute.For<IOutboxHandlerInvoker>();

        _repository
            .BeginUnitOfWorkAsync(Arg.Any<CancellationToken>())
            .Returns(_unitOfWork);
        _unitOfWork
            .ClaimNextAsync(Arg.Any<CancellationToken>())
            .Returns(GetOutboxMessage());
        _handlerInvoker
            .InvokeAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(new Success());

        _sut = new OutboxProcessor(
            _repository,
            _handlerInvoker,
            NullLogger<OutboxProcessor>.Instance);
    }

    [Fact]
    public async Task WhenNoMessageIsClaimed_ShouldRollbackAndReturnFalse()
    {
        _unitOfWork
            .ClaimNextAsync(Arg.Any<CancellationToken>())
            .Returns((OutboxMessage?)null);

        var sutResult = await _sut.ProcessNextAsync(CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _handlerInvoker.DidNotReceive().InvokeAsync(
            Arg.Any<OutboxMessage>(),
            Arg.Any<CancellationToken>());
        sutResult.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenHandlerSucceeds_ShouldCommitAndReturnTrue()
    {
        var sutResult = await _sut.ProcessNextAsync(CancellationToken.None);

        await _unitOfWork.Received(1).CommitAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
        await _handlerInvoker.Received(1).InvokeAsync(
            Arg.Is<OutboxMessage>(message => message.Id == GetOutboxMessage().Id),
            CancellationToken.None);
        sutResult.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenHandlerReturnsPermanentFailure_ShouldRollbackAndReturnTrue()
    {
        _handlerInvoker
            .InvokeAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(new Failure("outbox.permanent-error"));

        var sutResult = await _sut.ProcessNextAsync(CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        sutResult.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenHandlerReturnsTransientFailureAndCancellationIsRequested_ShouldRollbackBeforeDelay()
    {
        _handlerInvoker
            .InvokeAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(new Failure("outbox.transient-error", isTransient: true));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.ProcessNextAsync(new CancellationToken(canceled: true)));

        await _unitOfWork.Received(1).RollbackAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenHandlerThrowsOperationCanceledException_ShouldRethrow()
    {
        _handlerInvoker
            .InvokeAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.ProcessNextAsync(CancellationToken.None));

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenHandlerThrowsUnexpectedExceptionAndCancellationIsRequested_ShouldNotCommit()
    {
        _handlerInvoker
            .InvokeAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("handler failed"));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.ProcessNextAsync(new CancellationToken(canceled: true)));

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private static OutboxMessage GetOutboxMessage() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "{}",
        "Test.Type",
        "{}",
        new DateTime(2026, 9, 17, 18, 0, 0, DateTimeKind.Utc),
        new DateTimeOffset(2026, 9, 17, 18, 5, 0, TimeSpan.Zero));
}
