using global::Outbox.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Outbox.Tests.Infrastructure;

public sealed class OutboxHandlerInvokerTests
{
    private readonly IOutboxHandler _handler;
    private readonly OutboxHandlerInvoker _sut;

    public OutboxHandlerInvokerTests()
    {
        _handler = Substitute.For<IOutboxHandler>();
        _handler
            .HandleAsync(Arg.Any<TestPayload>(), Arg.Any<IDictionary<string, string>>())
            .Returns(new Success());

        var serviceProvider = new ServiceCollection()
            .AddSingleton(_handler)
            .BuildServiceProvider();

        _sut = new OutboxHandlerInvoker(serviceProvider);
    }

    [Fact]
    public async Task WhenMessageIsValid_ShouldInvokeHandlerAndReturnItsResult()
    {
        var sutArg = GetOutboxMessage();

        var sutResult = await _sut.InvokeAsync(sutArg, CancellationToken.None);

        await _handler.Received(1).HandleAsync(
            Arg.Is<TestPayload>(payload =>
                payload.Name == "feature-a" &&
                payload.Status == TestStatus.Enabled),
            Arg.Is<IDictionary<string, string>>(headers =>
                headers.Count == 1 &&
                headers["correlationId"] == "correlation-123"));
        sutResult.ShouldBeOfType<Success>();
        sutResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenPayloadTypeCannotBeResolved_ShouldReturnPayloadTypeNotFoundFailure()
    {
        var sutArg = GetOutboxMessage() with { Type = "Missing.Type, Missing.Assembly" };

        var sutResult = await _sut.InvokeAsync(sutArg, CancellationToken.None);

        var failure = sutResult.ShouldBeOfType<Failure>();
        failure.ErrorCode.ShouldBe(Vars.Errors.PayloadTypeNotFound);
        failure.IsPermanent.ShouldBeTrue();
        sutResult.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenHandlerIsNotRegistered_ShouldReturnHandlerNotRegisteredFailure()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var sut = new OutboxHandlerInvoker(serviceProvider);
        var sutArg = GetOutboxMessage();

        var sutResult = await sut.InvokeAsync(sutArg, CancellationToken.None);

        var failure = sutResult.ShouldBeOfType<Failure>();
        failure.ErrorCode.ShouldBe(Vars.Errors.HandlerNotRegistered);
        failure.IsPermanent.ShouldBeTrue();
        sutResult.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenPayloadDeserializesToNull_ShouldReturnPayloadDeserializationFailedFailure()
    {
        var sutArg = GetOutboxMessage() with { Data = "null" };

        var sutResult = await _sut.InvokeAsync(sutArg, CancellationToken.None);

        var failure = sutResult.ShouldBeOfType<Failure>();
        failure.ErrorCode.ShouldBe(Vars.Errors.PayloadDeserializationFailed);
        failure.IsPermanent.ShouldBeTrue();
        sutResult.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenHeadersDeserializeToNull_ShouldInvokeHandlerWithEmptyHeaders()
    {
        var sutArg = GetOutboxMessage() with { Headers = "null" };

        var sutResult = await _sut.InvokeAsync(sutArg, CancellationToken.None);

        await _handler.Received(1).HandleAsync(
            Arg.Any<TestPayload>(),
            Arg.Is<IDictionary<string, string>>(headers => headers.Count == 0));
        sutResult.IsSuccess.ShouldBeTrue();
    }

    private static OutboxMessage GetOutboxMessage() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        """{"name":"feature-a","status":"enabled"}""",
        typeof(TestPayload).AssemblyQualifiedName!,
        """{"correlationId":"correlation-123"}""",
        new DateTime(2026, 9, 17, 18, 0, 0, DateTimeKind.Utc),
        new DateTimeOffset(2026, 9, 17, 18, 5, 0, TimeSpan.Zero));

    public sealed record TestPayload(string Name, TestStatus Status);

    public enum TestStatus
    {
        Disabled,
        Enabled
    }
}
