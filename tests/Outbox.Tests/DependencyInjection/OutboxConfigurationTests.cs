using global::Outbox.DependencyInjection;
using global::Outbox.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Outbox.Tests.DependencyInjection;

public sealed class OutboxConfigurationTests
{
    private readonly ServiceCollection _services;
    private readonly OutboxConfiguration _sut;

    public OutboxConfigurationTests()
    {
        _services = [];
        _sut = new OutboxConfiguration(_services);
    }

    [Fact]
    public void WhenNoHandlerIsAdded_ShouldRegisterNullHandlerAsScopedOutboxHandler()
    {
        var sutResult = _services
            .Where(descriptor => descriptor.ServiceType == typeof(IOutboxHandler))
            .ShouldHaveSingleItem();
        sutResult.ImplementationType.ShouldBe(typeof(NullHandler));
        sutResult.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void WhenHandlerIsAdded_ShouldRegisterItAsScopedOutboxHandler()
    {
        _sut.AddHandler<TestOutboxHandler>();

        var sutResult = _services
            .Where(descriptor => descriptor.ServiceType == typeof(IOutboxHandler))
            .ShouldHaveSingleItem();
        sutResult.ImplementationType.ShouldBe(typeof(TestOutboxHandler));
        sutResult.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void WhenRepositoryIsConfiguredMoreThanOnce_ShouldKeepFirstRegistration()
    {
        var firstRepository = Substitute.For<IOutboxRepository>();
        var secondRepository = Substitute.For<IOutboxRepository>();
        _sut.UseRepository(_ => firstRepository);

        _sut.UseRepository(_ => secondRepository);

        using var serviceProvider = _services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sutResult = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        sutResult.ShouldBeSameAs(firstRepository);
    }

    [Fact]
    public void WhenRepositoryFactoryIsNull_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => _sut.UseRepository(null!));

        exception.ParamName.ShouldBe("repositoryFactory");
    }

    public sealed class TestOutboxHandler : IOutboxHandler
    {
        public Task<Result> HandleAsync<T>(T payload, IDictionary<string, string> headers)
        {
            return Task.FromResult<Result>(new Success());
        }
    }
}
