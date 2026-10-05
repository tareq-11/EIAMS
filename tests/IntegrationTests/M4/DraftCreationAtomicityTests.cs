using System.Net;
using System.Net.Http.Json;
using Domain.Common;
using Domain.DocumentLifecycleEvents;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M4;

/// <summary>Real PostgreSQL regression gate for failed draft aggregate creation.</summary>
public sealed class DraftCreationAtomicityTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public DraftCreationAtomicityTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CreateDraft_ShouldPersistCanonicalActorAndReturnResourceId()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid warehouseId = await SeedWarehouseAsync();
        await GrantDocumentCreateAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents", new
        {
            warehouseId,
            documentType = (int)DocumentType.Receiving
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiEnvelope<ResourceIdDto>? body = await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        body.ShouldNotBeNull();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = await context.WarehouseDocuments.AsNoTracking()
            .SingleAsync(item => item.Id == body.Data.Id);
        document.ReferenceSiteId.ShouldNotBeNull();
        document.ReferenceYear.ShouldBe(DateTime.UtcNow.Year);
        document.ReferenceSequence.ShouldNotBeNull();
        document.ReferenceSequence.Value.ShouldBeGreaterThan(0);
        document.CreatedBy.ShouldBe(userId);
        DocumentLifecycleEvent created = await context.DocumentLifecycleEvents.AsNoTracking()
            .SingleAsync(item => item.DocumentId == document.Id && item.Action == "Created");
        created.ActorUserId.ShouldBe(userId);
        created.ActorDisplayName.ShouldNotBeNullOrWhiteSpace();
        created.ResultingRowVersion.ShouldBe(document.RowVersion);
    }

    [Fact]
    public async Task CreateAdjustmentFromCount_ShouldRollbackDraftAndAllChildren_WhenChildValidationFails()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid countId = await SeedClosedCountWithInvalidVarianceAsync(userId);
        await GrantDocumentCreateAsync(userId, await GetCountWarehouseIdAsync(countId));
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsync(
            $"inventory-counts/{countId}/adjustment", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid warehouseId = await GetCountWarehouseIdAsync(countId);
        Guid[] adjustmentIds = await context.InventoryAdjustments.AsNoTracking()
            .Where(item => item.CountId == countId).Select(item => item.Id).ToArrayAsync();
        adjustmentIds.ShouldBeEmpty();
        (await context.WarehouseDocuments.CountAsync(item => item.WarehouseId == warehouseId &&
            item.DocumentType == DocumentType.Adjustment)).ShouldBe(0);
        (await context.DocumentLines.CountAsync(item => adjustmentIds.Contains(item.DocumentId))).ShouldBe(0);
        (await context.AdjustmentLines.CountAsync(item => adjustmentIds.Contains(item.AdjustmentId))).ShouldBe(0);
        (await context.DocumentLifecycleEvents.CountAsync(item => adjustmentIds.Contains(item.DocumentId))).ShouldBe(0);
        // The reference number allocation is raw SQL; it must participate in the same transaction too.
        Guid siteId = await context.Warehouses.Where(item => item.Id == warehouseId)
            .Select(item => item.SiteId).SingleAsync();
        (await context.DocumentSequences.CountAsync(item => item.SiteId == siteId &&
            item.Year == DateTime.UtcNow.Year)).ShouldBe(0);
    }

    private async Task<Guid> GetCountWarehouseIdAsync(Guid countId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.InventoryCounts.Where(item => item.Id == countId).Select(item => item.WarehouseId).SingleAsync();
    }

    private async Task GrantDocumentCreateAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"Draft atomicity role {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId));
        context.UserRoleScopes.RemoveRange(context.UserRoleScopes.Where(item => item.UserId == userId));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedClosedCountWithInvalidVarianceAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Atomicity org {suffix}", $"AO{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Atomicity site {suffix}", $"AS{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Atomicity warehouse {suffix}", $"AW{suffix}", "Test", true);
        var materialDomain = MaterialDomain.Create(Guid.NewGuid(), $"Atomicity domain {suffix}", $"AD{suffix}");
        var category = MaterialCategory.Create(Guid.NewGuid(), materialDomain.Id, null, $"Atomicity category {suffix}", $"AC{suffix}");
        var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, $"Atomicity family {suffix}", $"AF{suffix}");
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Atomicity unit {suffix}", $"au{suffix}", "Count");
        var material = Material.Create(
            Guid.NewGuid(), family.Id, unit.Id, $"Atomicity material {suffix}", null, $"AM{suffix}",
            MaterialKind.Consumable, TrackingType.Quantity, hasExpiry: false, attributes: null);
        DateTime now = DateTime.UtcNow;
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(), warehouse.Id, userId, InventoryCountType.Scheduled,
            InventoryCountScopeType.EntireWarehouse, null, FreezePolicy.NoFreeze, now).Value;
        InventoryCountLine line = InventoryCountLine.Create(Guid.NewGuid(), count.Id, material.Id, null, 1m).Value;
        line.RecordActual(2m);
        // Deliberately no variance reason. This is invalid for adjustment detail creation, while
        // the count itself can be seeded closed to exercise the handler's post-allocation failure.

        context.AddRange(organization, site, warehouse, materialDomain, category, family, unit, material, count, line);
        await context.SaveChangesAsync();

        count.Start(now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        count.Complete(now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        count.Close(now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        return count.Id;
    }

    private async Task<Guid> SeedWarehouseAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Draft org {suffix}", $"DO{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Draft site {suffix}", $"DS{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Draft warehouse {suffix}", $"DW{suffix}", "Test", true);
        context.AddRange(organization, site, warehouse);
        await context.SaveChangesAsync();
        return warehouse.Id;
    }

    private sealed record ResourceIdDto(Guid Id);
}
