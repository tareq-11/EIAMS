using Application.Abstractions.Numbering;
using Domain.Common;
using Domain.DocumentSequences;
using Domain.Organizations;
using Domain.Sites;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M2;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReferenceNumberConcurrencyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task AllocateAsync_Should_GenerateUniqueGapFreeNumbers_WhenCalledConcurrently()
    {
        Guid siteId = await SeedActiveSiteAsync();

        Task<Result<string>>[] allocations = Enumerable.Range(0, 12)
            .Select(_ => AllocateAsync(siteId))
            .ToArray();

        Result<string>[] results = await Task.WhenAll(allocations);

        results.ShouldAllBe(result => result.IsSuccess);
        string[] references = results.Select(result => result.Value).ToArray();
        references.Distinct().Count().ShouldBe(12);
        references
            .Select(reference =>
            {
                string[] segments = reference.Split('-');
                return int.Parse(segments[^1], System.Globalization.CultureInfo.InvariantCulture);
            })
            .Order()
            .ShouldBe(Enumerable.Range(1, 12));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DocumentSequence sequence = await dbContext.DocumentSequences.SingleAsync(item =>
            item.SiteId == siteId &&
            item.DocumentType == DocumentType.Receiving &&
            item.Year == DateTime.UtcNow.Year);

        sequence.LastSequence.ShouldBe(12);
    }

    private async Task<Result<string>> AllocateAsync(Guid siteId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IReferenceNumberGenerator generator = scope.ServiceProvider.GetRequiredService<IReferenceNumberGenerator>();

        return await generator.AllocateAsync(siteId, DocumentType.Receiving, CancellationToken.None);
    }

    private async Task<Guid> SeedActiveSiteAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"SITE{suffix}", null);

        dbContext.Organizations.Add(organization);
        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync();

        return site.Id;
    }
}
