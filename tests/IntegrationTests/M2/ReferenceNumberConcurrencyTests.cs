using Application.Abstractions.WarehouseDocuments;
using Domain.Common;
using Domain.DocumentSequences;
using Domain.WarehouseDocuments;
using Domain.Organizations;
using Domain.Sites;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M2;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReferenceNumberConcurrencyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task CreateDraftAsync_ShouldPersistUniqueSiteYearSequence_WhenCalledConcurrentlyAcrossTypes()
    {
        (Guid siteId, Guid warehouseId) = await SeedActiveWarehouseAsync();

        DocumentType[] types = Enum.GetValues<DocumentType>();
        Task<WarehouseDocument>[] creations = Enumerable.Range(0, 12)
            .Select(index => CreateDraftAsync(warehouseId, types[index % types.Length]))
            .ToArray();

        WarehouseDocument[] documents = await Task.WhenAll(creations);
        string[] references = documents.Select(document => document.SystemReferenceNumber).ToArray();
        references.Distinct().Count().ShouldBe(12);
        documents.Select(document => document.ReferenceSiteId).ShouldAllBe(id => id == siteId);
        documents.Select(document => document.ReferenceYear).ShouldAllBe(year => year == DateTime.UtcNow.Year);
        documents.Select(document => document.ReferenceSequence!.Value)
            .Order()
            .ToArray()
            .ShouldBe(Enumerable.Range(1, 12).ToArray());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WarehouseDocuments.AddRange(documents);
        await dbContext.SaveChangesAsync();

        (await dbContext.WarehouseDocuments.CountAsync(item => item.ReferenceSiteId == siteId &&
            item.ReferenceYear == DateTime.UtcNow.Year && item.ReferenceSequence != null)).ShouldBe(12);
        (await dbContext.DocumentSequences.CountAsync(item => item.SiteId == siteId &&
            item.Year == DateTime.UtcNow.Year)).ShouldBe(1);
        DocumentSequence sequence = await dbContext.DocumentSequences.SingleAsync(item =>
            item.SiteId == siteId && item.Year == DateTime.UtcNow.Year);

        sequence.LastSequence.ShouldBe(12);
    }

    private async Task<WarehouseDocument> CreateDraftAsync(Guid warehouseId, DocumentType type)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IWarehouseDocumentDraftFactory draftFactory = scope.ServiceProvider.GetRequiredService<IWarehouseDocumentDraftFactory>();

        Result<WarehouseDocument> result = await draftFactory.CreateAsync(warehouseId, type, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.ToString() : string.Empty);
        return result.Value;
    }

    private async Task<(Guid SiteId, Guid WarehouseId)> SeedActiveWarehouseAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"SITE{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"WH{suffix}", "Main", true);

        dbContext.AddRange(organization, site, warehouse);
        await dbContext.SaveChangesAsync();

        return (site.Id, warehouse.Id);
    }
}
