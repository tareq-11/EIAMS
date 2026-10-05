using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.ExternalParties;
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

public sealed class CompleteWarehouseDocumentDraftApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public CompleteWarehouseDocumentDraftApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CompleteDraft_ShouldReturnAuthoritativeAggregateWithLinesAndReceivingDetails()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents/complete-draft", new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[]
            {
                new { materialId = seed.MaterialId, quantity = 2m, unitId = seed.UnitId }
            },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = "INV-CD-1" }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement data = body.RootElement.GetProperty("data");
        Guid documentId = data.GetProperty("id").GetGuid();
        data.GetProperty("documentType").GetString().ShouldBe("Receiving");
        data.GetProperty("documentStatus").GetString().ShouldBe("Draft");
        data.GetProperty("rowVersion").GetInt32().ShouldBe(3);
        data.GetProperty("lines").GetArrayLength().ShouldBe(1);
        data.GetProperty("lines")[0].GetProperty("materialId").GetGuid().ShouldBe(seed.MaterialId);
        JsonElement receivingInfo = data.GetProperty("receivingInfo");
        receivingInfo.GetProperty("supplierInvoiceRef").GetString().ShouldBe("INV-CD-1");
        receivingInfo.GetProperty("receivingType").GetString().ShouldBe("Supplier");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument persisted = await context.WarehouseDocuments.AsNoTracking()
            .SingleAsync(document => document.Id == documentId);
        persisted.RowVersion.ShouldBe(3);
        (await context.DocumentLines.CountAsync(line => line.DocumentId == documentId)).ShouldBe(1);
        (await context.ReceivingInfos.AnyAsync(info => info.Id == documentId)).ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteDraft_ShouldRollbackHeaderSequenceAndEarlierLinesWhenLaterLineIsInvalid()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents/complete-draft", new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[]
            {
                new { materialId = seed.MaterialId, quantity = 1m, unitId = seed.UnitId },
                new { materialId = Guid.NewGuid(), quantity = 1m, unitId = seed.UnitId }
            },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = (string?)null }
        });

        response.IsSuccessStatusCode.ShouldBeFalse();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId &&
            document.DocumentType == DocumentType.Receiving)).ShouldBe(0);
        (await context.DocumentLines.CountAsync(line => line.MaterialId == seed.MaterialId)).ShouldBe(0);
        // The shared integration database can contain unrelated receiving documents from
        // other tests; absence of this warehouse's header also prevents a child detail row.
        Guid siteId = await context.Warehouses.Where(warehouse => warehouse.Id == seed.WarehouseId)
            .Select(warehouse => warehouse.SiteId).SingleAsync();
        (await context.DocumentSequences.CountAsync(sequence => sequence.SiteId == siteId &&
            sequence.Year == DateTime.UtcNow.Year)).ShouldBe(0);
    }

    [Fact]
    public async Task CompleteDraft_ShouldRequireCreateEditAndViewPermissions()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: false);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents/complete-draft", new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[]
            {
                new { materialId = seed.MaterialId, quantity = 1m, unitId = seed.UnitId }
            },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = (string?)null }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId)).ShouldBe(0);
    }

    [Fact]
    public async Task CompleteDraft_ShouldRejectAssetSelectionsForReceiving()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents/complete-draft", new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[]
            {
                new { materialId = seed.MaterialId, quantity = 1m, unitId = seed.UnitId, assetIds = new[] { Guid.NewGuid() } }
            },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = (string?)null }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("VALIDATION_GENERAL");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId)).ShouldBe(0);
    }

    [Fact]
    public async Task CompleteDraft_ShouldCreateQuantityAdjustmentAggregate()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-documents/complete-draft", new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Adjustment",
            lines = new[]
            {
                new { materialId = seed.MaterialId, difference = 1m, unitId = seed.UnitId }
            },
            adjustmentInfo = new { adjustmentKind = "Quantity", reason = "Count correction" }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("data").GetProperty("documentType").GetString().ShouldBe("Adjustment");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid id = body.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        (await context.InventoryAdjustments.AnyAsync(adjustment => adjustment.Id == id)).ShouldBeTrue();
        (await context.AdjustmentLines.AnyAsync(line => line.AdjustmentId == id && line.Difference == 1m)).ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteDraft_IdempotencyKeyShouldReplayAndRejectChangedPayload()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);
        var key = Guid.NewGuid();
        object payload = new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[] { new { materialId = seed.MaterialId, quantity = 2m, unitId = seed.UnitId } },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = "IDEM-1" }
        };

        HttpResponseMessage first = await PostWithIdempotencyKeyAsync(payload, key);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        using JsonDocument firstBody = await ReadJsonAsync(first);
        Guid firstId = firstBody.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        HttpResponseMessage replay = await PostWithIdempotencyKeyAsync(payload, key);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        using JsonDocument replayBody = await ReadJsonAsync(replay);
        replayBody.RootElement.GetProperty("data").GetProperty("id").GetGuid().ShouldBe(firstId);

        HttpResponseMessage changed = await PostWithIdempotencyKeyAsync(new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[] { new { materialId = seed.MaterialId, quantity = 3m, unitId = seed.UnitId } },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = "IDEM-1" }
        }, key);
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changed.Content.ReadAsStringAsync());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId && document.DocumentType == DocumentType.Receiving)).ShouldBe(1);
        (await context.IdempotencyRecords.CountAsync(record => record.Key == key)).ShouldBe(1);
    }

    [Fact]
    public async Task CompleteDraft_IdempotencyRecordAndSequenceShouldRollbackSoFailedKeyCanBeRetried()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CompleteDraftSeed seed = await SeedDependenciesAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId, includeEditAndView: true);
        Authenticate(tokens.AccessToken);
        var key = Guid.NewGuid();
        object invalidPayload = new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[]
            {
                new { materialId = seed.MaterialId, quantity = 1m, unitId = seed.UnitId },
                new { materialId = Guid.NewGuid(), quantity = 1m, unitId = seed.UnitId }
            },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = (string?)null }
        };
        (await PostWithIdempotencyKeyAsync(invalidPayload, key)).IsSuccessStatusCode.ShouldBeFalse();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.IdempotencyRecords.CountAsync(record => record.Key == key)).ShouldBe(0);
            (await context.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId)).ShouldBe(0);
            Guid siteId = await context.Warehouses.Where(warehouse => warehouse.Id == seed.WarehouseId)
                .Select(warehouse => warehouse.SiteId).SingleAsync();
            (await context.DocumentSequences.CountAsync(sequence => sequence.SiteId == siteId &&
                sequence.Year == DateTime.UtcNow.Year)).ShouldBe(0);
        }

        object validPayload = new
        {
            warehouseId = seed.WarehouseId,
            documentType = "Receiving",
            lines = new[] { new { materialId = seed.MaterialId, quantity = 1m, unitId = seed.UnitId } },
            receivingInfo = new { supplierPartyId = seed.SupplierId, supplierInvoiceRef = (string?)null }
        };
        HttpResponseMessage retry = await PostWithIdempotencyKeyAsync(validPayload, key);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        await using AsyncServiceScope verify = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyContext.IdempotencyRecords.CountAsync(record => record.Key == key)).ShouldBe(1);
        (await verifyContext.WarehouseDocuments.CountAsync(document => document.WarehouseId == seed.WarehouseId)).ShouldBe(1);
    }

    private async Task<CompleteDraftSeed> SeedDependenciesAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Complete org {suffix}", $"CO{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Complete site {suffix}", $"CS{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Complete warehouse {suffix}", $"CW{suffix}", "Test", true);
        var materialDomain = MaterialDomain.Create(Guid.NewGuid(), $"Complete domain {suffix}", $"CD{suffix}");
        var category = MaterialCategory.Create(
            Guid.NewGuid(), materialDomain.Id, null, $"Complete category {suffix}", $"CC{suffix}");
        var family = MaterialFamily.Create(
            Guid.NewGuid(), category.Id, $"Complete family {suffix}", $"CF{suffix}");
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Complete unit {suffix}", $"cu{suffix}", "Count");
        var material = Material.Create(
            Guid.NewGuid(), family.Id, unit.Id, $"Complete material {suffix}", null, $"CM{suffix}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);
        var supplier = ExternalParty.Create(
            Guid.NewGuid(), $"Complete supplier {suffix}", $"CSUP{suffix}", null, null);
        context.AddRange(organization, site, warehouse, materialDomain, category, family, unit, material, supplier);
        await context.SaveChangesAsync();
        return new CompleteDraftSeed(warehouse.Id, material.Id, unit.Id, supplier.Id);
    }

    private async Task GrantDocumentPermissionsAsync(Guid userId, Guid warehouseId, bool includeEditAndView)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"Complete draft role {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId));
        if (includeEditAndView)
        {
            context.RolePermissions.AddRange(
                RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentUpdateId),
                RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentViewId));
        }
        context.UserRoleScopes.RemoveRange(context.UserRoleScopes.Where(item => item.UserId == userId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> PostWithIdempotencyKeyAsync(object payload, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "warehouse-documents/complete-draft")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return await HttpClient.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private sealed record CompleteDraftSeed(Guid WarehouseId, Guid MaterialId, Guid UnitId, Guid SupplierId);
}
