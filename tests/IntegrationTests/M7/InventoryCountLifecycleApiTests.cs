using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.InventoryBalances;
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
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.M7;

[Collection(nameof(IntegrationTestCollection))]
public sealed class InventoryCountLifecycleApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public InventoryCountLifecycleApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetLines_AfterStart_ShouldReturnPagedSnapshot()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CountSeed seed = await SeedCountableWarehouseAsync();
        await GrantInventoryCountPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid countId = await PlanSelectedCountAsync(seed);

        HttpResponseMessage start = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/start", new { expectedRowVersion = 1 });
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());

        HttpResponseMessage firstPage = await HttpClient.GetAsync(
            $"inventory-counts/{countId}/lines?page=1&pageSize=1");
        string firstPageBody = await firstPage.Content.ReadAsStringAsync();
        firstPage.StatusCode.ShouldBe(HttpStatusCode.OK, firstPageBody);
        using var firstPageJson = JsonDocument.Parse(firstPageBody);
        JsonElement firstRoot = firstPageJson.RootElement;
        firstRoot.GetProperty("success").GetBoolean().ShouldBeTrue();
        firstRoot.GetProperty("data").GetArrayLength().ShouldBe(1);
        firstRoot.GetProperty("pagination").GetProperty("page").GetInt32().ShouldBe(1);
        firstRoot.GetProperty("pagination").GetProperty("page_size").GetInt32().ShouldBe(1);
        firstRoot.GetProperty("pagination").GetProperty("total_items").GetInt32().ShouldBe(1);

        HttpResponseMessage defaultSizedPage = await HttpClient.GetAsync(
            $"inventory-counts/{countId}/lines?page=1&pageSize=20");
        string defaultSizedPageBody = await defaultSizedPage.Content.ReadAsStringAsync();
        defaultSizedPage.StatusCode.ShouldBe(HttpStatusCode.OK, defaultSizedPageBody);
        using var defaultSizedPageJson = JsonDocument.Parse(defaultSizedPageBody);
        defaultSizedPageJson.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
        defaultSizedPageJson.RootElement.GetProperty("pagination").GetProperty("page_size").GetInt32().ShouldBe(20);

        HttpResponseMessage secondPage = await HttpClient.GetAsync(
            $"inventory-counts/{countId}/lines?page=2&pageSize=1");
        string secondPageBody = await secondPage.Content.ReadAsStringAsync();
        secondPage.StatusCode.ShouldBe(HttpStatusCode.OK, secondPageBody);
        using var secondPageJson = JsonDocument.Parse(secondPageBody);
        secondPageJson.RootElement.GetProperty("data").GetArrayLength().ShouldBe(0);
        secondPageJson.RootElement.GetProperty("pagination").GetProperty("page").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Start_ShouldSnapshotCurrentMembership_AndDatabaseRejectsAddRemoveOrMutation()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CountSeed seed = await SeedCountableWarehouseAsync();
        await GrantInventoryCountPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid countId = await PlanSelectedCountAsync(seed);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.InventoryCountLines.CountAsync(line => line.CountId == countId)).ShouldBe(0);
            // Simulate an older Planned count that already has a snapshot from Plan time.
            context.InventoryCountLines.Add(InventoryCountLine.Create(
                Guid.NewGuid(), countId, seed.MaterialId, null, 5m).Value);
            InventoryBalance balance = await context.InventoryBalances.SingleAsync(item =>
                item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId);
            balance.SetQuantity(7m, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync();
        }

        HttpResponseMessage start = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/start", new { expectedRowVersion = 1 });
        start.StatusCode.ShouldBe(HttpStatusCode.OK);

        Guid lineId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            InventoryCount count = await context.InventoryCounts.SingleAsync(item => item.Id == countId);
            count.Status.ShouldBe(InventoryCountStatus.InProgress);
            count.RowVersion.ShouldBe(2);
            InventoryCountLine line = await context.InventoryCountLines.SingleAsync(item => item.CountId == countId);
            line.SnapshotQuantity.ShouldBe(7m);
            lineId = line.Id;
        }

        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();

        await Should.ThrowAsync<PostgresException>(async () =>
            await ExecuteAsync(connection,
                "INSERT INTO public.inventory_count_lines (id,count_id,material_id,snapshot_quantity,created_at_utc) " +
                "VALUES (@id,@count,@material,0,NOW())",
                ("id", Guid.NewGuid()), ("count", countId), ("material", seed.MaterialId)));
        await Should.ThrowAsync<PostgresException>(async () =>
            await ExecuteAsync(connection,
                "DELETE FROM public.inventory_count_lines WHERE id=@id", ("id", lineId)));
        await Should.ThrowAsync<PostgresException>(async () =>
            await ExecuteAsync(connection,
                "UPDATE public.inventory_count_lines SET snapshot_quantity=8 WHERE id=@id", ("id", lineId)));
        await Should.ThrowAsync<PostgresException>(async () =>
            await ExecuteAsync(connection,
                "DELETE FROM public.inventory_count_scope_materials WHERE count_id=@count",
                ("count", countId)));

        (await GetInventoryCountLineCountAsync(countId)).ShouldBe(1);
    }

    [Fact]
    public async Task Cancel_ShouldAbortPlannedOrStartedCounts_AndRejectFurtherTransitions()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CountSeed seed = await SeedCountableWarehouseAsync();
        await GrantInventoryCountPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        Guid plannedCountId = await PlanSelectedCountAsync(seed);
        await AssertDatabaseRejectsStartWithoutSnapshotAsync(plannedCountId);
        HttpResponseMessage cancelPlanned = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{plannedCountId}/cancel", new { expectedRowVersion = 1 });
        cancelPlanned.StatusCode.ShouldBe(HttpStatusCode.OK);
        await AssertCountStatusAsync(plannedCountId, InventoryCountStatus.Aborted, expectedRowVersion: 2);

        HttpResponseMessage startAborted = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{plannedCountId}/start", new { expectedRowVersion = 2 });
        startAborted.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            await startAborted.Content.ReadAsStringAsync());

        Guid startedCountId = await PlanSelectedCountAsync(seed);
        HttpResponseMessage start = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{startedCountId}/start", new { expectedRowVersion = 1 });
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage cancelStarted = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{startedCountId}/cancel", new { expectedRowVersion = 2 });
        cancelStarted.StatusCode.ShouldBe(HttpStatusCode.OK);
        await AssertCountStatusAsync(startedCountId, InventoryCountStatus.Aborted, expectedRowVersion: 3);

        HttpResponseMessage lateActual = await HttpClient.PutAsJsonAsync(
            $"inventory-counts/{startedCountId}/lines/{await GetLineIdAsync(startedCountId)}/actual",
            new { actualQuantity = 7m, expectedRowVersion = 3 });
        lateActual.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Close_ShouldBeTerminalAfterActualsAndCompletion()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CountSeed seed = await SeedCountableWarehouseAsync();
        await GrantInventoryCountPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid countId = await PlanSelectedCountAsync(seed);
        (await HttpClient.PostAsJsonAsync($"inventory-counts/{countId}/start", new { expectedRowVersion = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        Guid lineId = await GetLineIdAsync(countId);
        (await HttpClient.PutAsJsonAsync(
            $"inventory-counts/{countId}/lines/{lineId}/actual",
            new { actualQuantity = 7m, expectedRowVersion = 2 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.PutAsJsonAsync(
            $"inventory-counts/{countId}/lines/{lineId}/variance-reason",
            new { reason = "Verified test variance", expectedRowVersion = 3 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/complete", new { expectedRowVersion = 4 }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage close = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/close", new { expectedRowVersion = 5 });
        close.StatusCode.ShouldBe(HttpStatusCode.OK, await close.Content.ReadAsStringAsync());
        await AssertCountStatusAsync(countId, InventoryCountStatus.Closed, expectedRowVersion: 6);

        HttpResponseMessage cancelClosed = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/cancel", new { expectedRowVersion = 6 });
        cancelClosed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentStarts_ShouldAllowOnlyOneCountForWarehouse()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        CountSeed seed = await SeedCountableWarehouseAsync();
        await GrantInventoryCountPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid firstCountId = await PlanSelectedCountAsync(seed);
        Guid secondCountId = await PlanSelectedCountAsync(seed);

        HttpResponseMessage[] starts = await Task.WhenAll(
            HttpClient.PostAsJsonAsync($"inventory-counts/{firstCountId}/start", new { expectedRowVersion = 1 }),
            HttpClient.PostAsJsonAsync($"inventory-counts/{secondCountId}/start", new { expectedRowVersion = 1 }));

        starts.Count(response => response.IsSuccessStatusCode).ShouldBe(1);
        starts.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.InventoryCounts.CountAsync(item => item.WarehouseId == seed.WarehouseId &&
            item.Status == InventoryCountStatus.InProgress)).ShouldBe(1);
    }

    private async Task<CountSeed> SeedCountableWarehouseAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Count Org {suffix}", $"CO{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Count Site {suffix}", $"CS{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Count Warehouse {suffix}", $"CW{suffix}", "Main", true);
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Count Unit {suffix}", $"CU{suffix}", "Count");
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Count Domain {suffix}", $"CD{suffix}");
        var category = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, $"Count Category {suffix}", $"CC{suffix}");
        var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, $"Count Family {suffix}", $"CF{suffix}");
        var material = Material.Create(Guid.NewGuid(), family.Id, unit.Id, $"Count Material {suffix}", null,
            $"CM{suffix}", MaterialKind.Consumable, TrackingType.Quantity, false, null);
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouse.Id, domain.Id);
        var balance = InventoryBalance.CreateZero(Guid.NewGuid(), warehouse.Id, material.Id, DateTime.UtcNow);
        balance.SetQuantity(5m, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        context.AddRange(organization, site, warehouse, unit, domain, category, family, material,
            capability, WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Count), balance);
        await context.SaveChangesAsync();
        return new CountSeed(warehouse.Id, material.Id);
    }

    private async Task GrantInventoryCountPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 count lifecycle {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountViewId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountPlanId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountEnterId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountCompleteId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountCloseId));
        await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> PlanSelectedCountAsync(CountSeed seed)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("inventory-counts", new
        {
            warehouseId = seed.WarehouseId,
            countType = "Surprise",
            scopeType = "SelectedMaterials",
            materialIds = new[] { seed.MaterialId },
            freezePolicy = "NoFreeze"
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        ApiEnvelope<ResourceId>? envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceId>>();
        envelope.ShouldNotBeNull();
        return envelope.Data.Id;
    }

    private async Task<Guid> GetLineIdAsync(Guid countId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.InventoryCountLines.Where(line => line.CountId == countId)
            .Select(line => line.Id).SingleAsync();
    }

    private async Task<int> GetInventoryCountLineCountAsync(Guid countId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.InventoryCountLines.CountAsync(line => line.CountId == countId);
    }

    private async Task AssertDatabaseRejectsStartWithoutSnapshotAsync(Guid countId)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await Should.ThrowAsync<PostgresException>(async () =>
            await ExecuteAsync(connection,
                "UPDATE public.inventory_counts SET status='InProgress', started_at_utc=planned_at_utc, " +
                "row_version=row_version+1 WHERE id=@id",
                ("id", countId)));
    }

    private async Task AssertCountStatusAsync(Guid countId, InventoryCountStatus expectedStatus, int expectedRowVersion)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryCount count = await context.InventoryCounts.SingleAsync(item => item.Id == countId);
        count.Status.ShouldBe(expectedStatus);
        count.RowVersion.ShouldBe(expectedRowVersion);
        if (expectedStatus == InventoryCountStatus.Aborted)
        {
            count.AbortedAtUtc.ShouldNotBeNull();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Call sites pass fixed test SQL strings; values are always bound as Npgsql parameters.")]
    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private sealed record CountSeed(Guid WarehouseId, Guid MaterialId);
    private sealed record ResourceId(Guid Id);
}
