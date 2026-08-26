using Application.Abstractions.Audit;
using Application.Abstractions.Behaviors;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.UnitTests.M8;

public sealed class AuditCommandDecoratorTests
{
    [Fact]
    public async Task Handle_Should_OpenScopeWithExpectedMetadata_WhenResultCommandExecutes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        FakeAuditOperationContextAccessor accessor = new();
        StubRequestAuditContext requestAudit = new("request-42");
        StubUserContext userContext = new(userId);

        AuditOperationDescriptor? observed = null;
        ICommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse> inner =
            Substitute.For<ICommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>>();
        inner
            .Handle(Arg.Any<DecoratorProbeResponseCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                observed = accessor.Current;

                return Task.FromResult(Result.Success(new DecoratorProbeResponse()));
            });

        var decorator = new AuditCommandDecorator.CommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>(
            accessor,
            requestAudit,
            userContext,
            inner);

        // Act
        Result<DecoratorProbeResponse> result =
            await decorator.Handle(new DecoratorProbeResponseCommand("value"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        observed.ShouldNotBeNull();
        observed.CommandName.ShouldBe(nameof(DecoratorProbeResponseCommand));
        observed.UserId.ShouldBe(userId);
        observed.RequestId.ShouldBe("request-42");
        observed.IpAddress.ShouldBe("192.168.1.10");
        observed.Kind.ShouldBe(AuditOperationKind.Http);
        accessor.BeginScopeCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_CloseScopeAfterCompletion_WhenResultCommandExecutes()
    {
        // Arrange
        FakeAuditOperationContextAccessor accessor = new();
        var decorator = new AuditCommandDecorator.CommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>(
            accessor,
            new StubRequestAuditContext("request-42"),
            new StubUserContext(Guid.NewGuid()),
            Substitute.For<ICommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>>());

        // Act
        await decorator.Handle(new DecoratorProbeResponseCommand("value"), CancellationToken.None);

        // Assert
        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ClassifyAnonymousKind_WhenUserIdIsMissingButRequestIdIsPresent()
    {
        // Arrange
        FakeAuditOperationContextAccessor accessor = new();

        AuditOperationDescriptor? observed = null;
        ICommandHandler<DecoratorProbeBaseCommand> inner = Substitute.For<ICommandHandler<DecoratorProbeBaseCommand>>();
        inner
            .Handle(Arg.Any<DecoratorProbeBaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                observed = accessor.Current;

                return Task.FromResult(Result.Success());
            });

        var decorator = new AuditCommandDecorator.CommandBaseHandler<DecoratorProbeBaseCommand>(
            accessor,
            new StubRequestAuditContext("request-42"),
            new StubUserContext(null),
            inner);

        // Act
        Result result = await decorator.Handle(new DecoratorProbeBaseCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        observed.ShouldNotBeNull();
        observed.Kind.ShouldBe(AuditOperationKind.Anonymous);
        observed.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ClassifyBackgroundKind_WhenNoRequestIdIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        FakeAuditOperationContextAccessor accessor = new();

        AuditOperationDescriptor? observed = null;
        ICommandHandler<DecoratorProbeBaseCommand> inner = Substitute.For<ICommandHandler<DecoratorProbeBaseCommand>>();
        inner
            .Handle(Arg.Any<DecoratorProbeBaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                observed = accessor.Current;

                return Task.FromResult(Result.Success());
            });

        var decorator = new AuditCommandDecorator.CommandBaseHandler<DecoratorProbeBaseCommand>(
            accessor,
            new StubRequestAuditContext(null),
            new StubUserContext(userId),
            inner);

        // Act
        Result result = await decorator.Handle(new DecoratorProbeBaseCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        observed.ShouldNotBeNull();
        observed.Kind.ShouldBe(AuditOperationKind.Background);
        observed.RequestId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_DisposeScopeAndRethrowException_WhenInnerHandlerThrows()
    {
        // Arrange
        FakeAuditOperationContextAccessor accessor = new();
        var expectedException = new InvalidOperationException("boom");
        ICommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse> inner =
            Substitute.For<ICommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>>();
        inner
            .Handle(Arg.Any<DecoratorProbeResponseCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Result<DecoratorProbeResponse>>(expectedException));

        var decorator = new AuditCommandDecorator.CommandHandler<DecoratorProbeResponseCommand, DecoratorProbeResponse>(
            accessor,
            new StubRequestAuditContext("request-42"),
            new StubUserContext(Guid.NewGuid()),
            inner);

        // Act
        InvalidOperationException actual = await Should.ThrowAsync<InvalidOperationException>(
            () => decorator.Handle(new DecoratorProbeResponseCommand("value"), CancellationToken.None));

        // Assert
        actual.ShouldBeSameAs(expectedException);
        accessor.Current.ShouldBeNull();
    }
}

public sealed record DecoratorProbeBaseCommand : ICommand;

public sealed record DecoratorProbeResponseCommand(string Value) : ICommand<DecoratorProbeResponse>;

public sealed record DecoratorProbeResponse;
