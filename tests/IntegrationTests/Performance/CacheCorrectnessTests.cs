using Application.Abstractions.Messaging;
using Application.Abstractions.Data;
using Application.Organizations.GetById;
using Domain.Organizations;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class CacheCorrectnessTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public CacheCorrectnessTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task OrganizationCache_Should_ReturnFreshData_AfterMutation()
    {
        // Arrange: persist and cache the original value through the real query handler.
        var organizationId = Guid.NewGuid();
        string code = $"CACHE-{Guid.NewGuid():N}"[..16];

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Organizations.Add(Organization.Create(organizationId, "Before update", code));
            await context.SaveChangesAsync();

            IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>>();
            Result<OrganizationResponse> cached = await handler.Handle(
                new GetOrganizationByIdQuery(organizationId),
                CancellationToken.None);

            cached.IsSuccess.ShouldBeTrue();
            cached.Value.Name.ShouldBe("Before update");
        }

        // Act: mutate through EF Core; SaveChanges must invalidate the entity tag.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Organization organization = await context.Organizations.SingleAsync(item => item.Id == organizationId);
            organization.UpdateDetails("After update");
            await context.SaveChangesAsync();
        }

        // Assert: a new handler scope must not receive the stale cached projection.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>>();
            Result<OrganizationResponse> refreshed = await handler.Handle(
                new GetOrganizationByIdQuery(organizationId),
                CancellationToken.None);

            refreshed.IsSuccess.ShouldBeTrue();
            refreshed.Value.Name.ShouldBe("After update");
        }
    }

    [Fact]
    public async Task OrganizationCache_Should_InvalidateOnlyAfterTransactionCommits()
    {
        var organizationId = Guid.NewGuid();
        string code = $"TX-CACHE-{Guid.NewGuid():N}"[..16];

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Organizations.Add(Organization.Create(organizationId, "Before transaction", code));
            await context.SaveChangesAsync();

            IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>>();
            Result<OrganizationResponse> cached = await handler.Handle(
                new GetOrganizationByIdQuery(organizationId),
                CancellationToken.None);
            cached.Value.Name.ShouldBe("Before transaction");
        }

        await using (AsyncServiceScope mutationScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IApplicationTransaction transaction = mutationScope.ServiceProvider
                .GetRequiredService<IApplicationTransaction>();

            Result result = await transaction.ExecuteAsync(
                async cancellationToken =>
                {
                    Organization organization = await context.Organizations
                        .SingleAsync(item => item.Id == organizationId, cancellationToken);
                    organization.UpdateDetails("After transaction");
                    await context.SaveChangesAsync(cancellationToken);

                    await using AsyncServiceScope concurrentScope = factory.Services.CreateAsyncScope();
                    IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> concurrentHandler =
                        concurrentScope.ServiceProvider
                            .GetRequiredService<IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>>();
                    Result<OrganizationResponse> beforeCommit = await concurrentHandler.Handle(
                        new GetOrganizationByIdQuery(organizationId),
                        cancellationToken);
                    beforeCommit.Value.Name.ShouldBe("Before transaction");

                    return Result.Success();
                },
                CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
        }

        await using (AsyncServiceScope verificationScope = factory.Services.CreateAsyncScope())
        {
            IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler = verificationScope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>>();
            Result<OrganizationResponse> refreshed = await handler.Handle(
                new GetOrganizationByIdQuery(organizationId),
                CancellationToken.None);

            refreshed.Value.Name.ShouldBe("After transaction");
        }
    }
}
