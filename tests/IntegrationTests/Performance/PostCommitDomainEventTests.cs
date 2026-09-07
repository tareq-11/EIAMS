using Application.Abstractions.Data;
using Domain.Organizations;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PostCommitDomainEventTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task Transaction_Should_DispatchDomainEventsOnlyAfterCommit()
    {
        var dispatcher = new RecordingDomainEventsDispatcher();
        await using ApplicationDbContext context = CreateContext(dispatcher);
        var transaction = new EfApplicationTransaction(context);
        var organizationId = Guid.NewGuid();

        Result result = await transaction.ExecuteAsync(
            async cancellationToken =>
            {
                context.Organizations.Add(Organization.Create(
                    organizationId,
                    "Post-commit organization",
                    $"PC-{Guid.NewGuid():N}"[..16]));
                await context.SaveChangesAsync(cancellationToken);

                dispatcher.Events.ShouldBeEmpty();
                return Result.Success();
            },
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        dispatcher.Events.ShouldHaveSingleItem()
            .ShouldBeOfType<OrganizationCreatedDomainEvent>()
            .OrganizationId.ShouldBe(organizationId);
    }

    [Fact]
    public async Task Transaction_Should_DiscardDomainEventsAfterRollback()
    {
        var dispatcher = new RecordingDomainEventsDispatcher();
        await using ApplicationDbContext context = CreateContext(dispatcher);
        var transaction = new EfApplicationTransaction(context);

        Result result = await transaction.ExecuteAsync(
            async cancellationToken =>
            {
                context.Organizations.Add(Organization.Create(
                    Guid.NewGuid(),
                    "Rolled-back organization",
                    $"RB-{Guid.NewGuid():N}"[..16]));
                await context.SaveChangesAsync(cancellationToken);

                return Result.Failure(Error.Conflict("Test.Rollback", "Rollback requested."));
            },
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        dispatcher.Events.ShouldBeEmpty();
    }

    private ApplicationDbContext CreateContext(IDomainEventsDispatcher dispatcher)
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(factory.DatabaseConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options;

        return new ApplicationDbContext(options, dispatcher);
    }

    private sealed class RecordingDomainEventsDispatcher : IDomainEventsDispatcher
    {
        internal List<IDomainEvent> Events { get; } = [];

        public Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }
}
