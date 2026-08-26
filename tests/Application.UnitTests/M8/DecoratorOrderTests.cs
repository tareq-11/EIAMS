using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Application.UnitTests.M8;

public sealed class DecoratorOrderTests
{
    [Fact]
    public async Task Pipeline_Should_RunHandlerInsideOpenAuditScope_WhenValidationPasses()
    {
        // Arrange
        List<string> events = [];
        using ServiceProvider provider = BuildProvider(events);
        ICommandHandler<OrderProbeCommand> handler =
            provider.GetRequiredService<ICommandHandler<OrderProbeCommand>>();

        // Act
        Result result = await handler.Handle(new OrderProbeCommand(FailValidation: false), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        events.Count.ShouldBe(2);
        events[0].ShouldBe("validation:no-audit-scope");
        events[1].ShouldBe($"handler:audit-scope:{nameof(OrderProbeCommand)}");
    }

    [Fact]
    public async Task Pipeline_Should_ShortCircuitWithoutOpeningAuditScope_WhenValidationFails()
    {
        // Arrange
        List<string> events = [];
        using ServiceProvider provider = BuildProvider(events);
        ICommandHandler<OrderProbeCommand> handler =
            provider.GetRequiredService<ICommandHandler<OrderProbeCommand>>();

        // Act
        Result result = await handler.Handle(new OrderProbeCommand(FailValidation: true), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        events.ShouldHaveSingleItem().ShouldBe("validation:no-audit-scope");
    }

    [Fact]
    public async Task ResultPipeline_Should_RunHandlerInsideOpenAuditScope_WhenValidationPasses()
    {
        // Arrange
        List<string> events = [];
        using ServiceProvider provider = BuildProvider(events);
        ICommandHandler<OrderProbeResultCommand, OrderProbeResponse> handler =
            provider.GetRequiredService<ICommandHandler<OrderProbeResultCommand, OrderProbeResponse>>();

        // Act
        Result<OrderProbeResponse> result =
            await handler.Handle(new OrderProbeResultCommand(FailValidation: false), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        events.Count.ShouldBe(2);
        events[0].ShouldBe("validation:no-audit-scope");
        events[1].ShouldBe($"handler:audit-scope:{nameof(OrderProbeResultCommand)}");
    }

    private static ServiceProvider BuildProvider(List<string> events)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<FakeAuditOperationContextAccessor>();
        services.AddSingleton<IAuditOperationContextAccessor>(sp =>
            sp.GetRequiredService<FakeAuditOperationContextAccessor>());
        services.AddSingleton<IRequestAuditContext>(new StubRequestAuditContext("request-7"));
        services.AddSingleton<IUserContext>(new StubUserContext(Guid.NewGuid()));

        services.AddScoped<IValidator<OrderProbeCommand>>(sp =>
            new OrderProbeValidator(events, sp.GetRequiredService<IAuditOperationContextAccessor>()));
        services.AddScoped<IValidator<OrderProbeResultCommand>>(sp =>
            new OrderProbeResultValidator(events, sp.GetRequiredService<IAuditOperationContextAccessor>()));

        services.AddScoped<ICommandHandler<OrderProbeCommand>>(sp =>
            new OrderProbeHandler(events, sp.GetRequiredService<IAuditOperationContextAccessor>()));
        services.AddScoped<ICommandHandler<OrderProbeResultCommand, OrderProbeResponse>>(sp =>
            new OrderProbeResultHandler(events, sp.GetRequiredService<IAuditOperationContextAccessor>()));

        services.AddApplication();

        return services.BuildServiceProvider();
    }

    private sealed record OrderProbeCommand(bool FailValidation) : ICommand;

    private sealed record OrderProbeResultCommand(bool FailValidation) : ICommand<OrderProbeResponse>;

    private sealed record OrderProbeResponse;

    private sealed class OrderProbeValidator : AbstractValidator<OrderProbeCommand>
    {
        private readonly List<string> events;
        private readonly IAuditOperationContextAccessor accessor;

        public OrderProbeValidator(List<string> events, IAuditOperationContextAccessor accessor)
        {
            this.events = events;
            this.accessor = accessor;

            RuleFor(command => command.FailValidation)
                .Equal(false)
                .WithErrorCode("ORDER_PROBE_INVALID");
        }

        public override Task<ValidationResult> ValidateAsync(
            ValidationContext<OrderProbeCommand> context,
            CancellationToken cancellation = default)
        {
            events.Add(accessor.Current is null ? "validation:no-audit-scope" : "validation:audit-scope-open");

            return base.ValidateAsync(context, cancellation);
        }
    }

    private sealed class OrderProbeResultValidator : AbstractValidator<OrderProbeResultCommand>
    {
        private readonly List<string> events;
        private readonly IAuditOperationContextAccessor accessor;

        public OrderProbeResultValidator(List<string> events, IAuditOperationContextAccessor accessor)
        {
            this.events = events;
            this.accessor = accessor;

            RuleFor(command => command.FailValidation)
                .Equal(false)
                .WithErrorCode("ORDER_PROBE_RESULT_INVALID");
        }

        public override Task<ValidationResult> ValidateAsync(
            ValidationContext<OrderProbeResultCommand> context,
            CancellationToken cancellation = default)
        {
            events.Add(accessor.Current is null ? "validation:no-audit-scope" : "validation:audit-scope-open");

            return base.ValidateAsync(context, cancellation);
        }
    }

    private sealed class OrderProbeHandler(List<string> events, IAuditOperationContextAccessor accessor)
        : ICommandHandler<OrderProbeCommand>
    {
        public Task<Result> Handle(OrderProbeCommand command, CancellationToken cancellationToken)
        {
            events.Add(accessor.Current is null
                ? "handler:no-audit-scope"
                : $"handler:audit-scope:{accessor.Current.CommandName}");

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class OrderProbeResultHandler(List<string> events, IAuditOperationContextAccessor accessor)
        : ICommandHandler<OrderProbeResultCommand, OrderProbeResponse>
    {
        public Task<Result<OrderProbeResponse>> Handle(
            OrderProbeResultCommand command,
            CancellationToken cancellationToken)
        {
            events.Add(accessor.Current is null
                ? "handler:no-audit-scope"
                : $"handler:audit-scope:{accessor.Current.CommandName}");

            return Task.FromResult(Result.Success(new OrderProbeResponse()));
        }
    }
}
