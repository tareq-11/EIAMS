using System.Net;
using System.Net.Http.Json;
using Application.Custodies.GetCustodies;
using Application.Returns.GetEligibleItems;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DurableCustodyAllocations;
using Domain.Employees;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Web.Api.Controllers.Custodies;
using Web.Api.Infrastructure;

namespace IntegrationTests.Phase8;

public sealed class DurableCustodyIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public DurableCustodyIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetCustodies_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("custodies");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCustodies_Should_ReturnCustodies_WhenAuthenticated()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        // Seed data
        Guid siteId = await SeedSiteAsync();
        Guid warehouseId = await SeedWarehouseAsync(siteId);
        Guid unitId = await SeedUnitOfMeasureAsync();
        Guid materialId = await SeedMaterialAsync(unitId, MaterialKind.Durable, TrackingType.Quantity);
        Guid employeeId = await SeedEmployeeAsync(siteId);
        Guid issueDocId = await SeedPostedIssueDocumentAsync(warehouseId, userId);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DurableCustodyAllocation alloc = DurableCustodyAllocation.Open(
                Guid.NewGuid(),
                materialId,
                warehouseId,
                PartyType.Employee,
                employeeId,
                CustodyKind.Personal,
                issueDocId,
                5m,
                DateTime.UtcNow).Value;

            dbContext.DurableCustodyAllocations.Add(alloc);
            await dbContext.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("custodies");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiResponse<IReadOnlyList<CustodyResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<CustodyResponse>>>();
        envelope.ShouldNotBeNull();
        envelope.Data.ShouldNotBeNull();
        envelope.Data.ShouldContain(c => c.MaterialId == materialId && c.IssuedQuantity == 5m);
    }

    [Fact]
    public async Task TransferCustody_Should_TransferDurableAllocation_ToNewHolder()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        Guid siteId = await SeedSiteAsync();
        Guid warehouseId = await SeedWarehouseAsync(siteId);
        Guid unitId = await SeedUnitOfMeasureAsync();
        Guid materialId = await SeedMaterialAsync(unitId, MaterialKind.Durable, TrackingType.Quantity);
        Guid employeeId = await SeedEmployeeAsync(siteId);
        Guid issueDocId = await SeedPostedIssueDocumentAsync(warehouseId, userId);

        var allocId = Guid.NewGuid();
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DurableCustodyAllocation alloc = DurableCustodyAllocation.Open(
                allocId,
                materialId,
                warehouseId,
                PartyType.Employee,
                employeeId,
                CustodyKind.Personal,
                issueDocId,
                10m,
                DateTime.UtcNow).Value;

            dbContext.DurableCustodyAllocations.Add(alloc);
            await dbContext.SaveChangesAsync();
        }

        // Act: Transfer to site
        var transferRequest = new TransferCustodyRequest(
            CustodySubjectType.MaterialQuantity,
            PartyType.Site,
            siteId,
            CustodyKind.Operational,
            1,
            "Transfer to site storage");

        HttpResponseMessage transferResponse = await HttpClient.PostAsJsonAsync(
            $"custodies/{allocId}/transfer",
            transferRequest);

        string errorContent = await transferResponse.Content.ReadAsStringAsync();
        transferResponse.StatusCode.ShouldBe(HttpStatusCode.OK, errorContent);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DurableCustodyAllocation? updated = await dbContext.DurableCustodyAllocations
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == allocId);

            updated.ShouldNotBeNull();
            updated.HolderType.ShouldBe(PartyType.Site);
            updated.HolderId.ShouldBe(siteId);
            updated.CustodyKind.ShouldBe(CustodyKind.Operational);
            updated.RowVersion.ShouldBe(2);

            bool historyExists = await dbContext.DurableCustodyHistories
                .AnyAsync(h => h.SubjectId == allocId && h.Action == "Transferred");
            historyExists.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetReturnEligibleItems_Should_ReturnActiveAllocations_ForOriginalIssue()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        Guid siteId = await SeedSiteAsync();
        Guid warehouseId = await SeedWarehouseAsync(siteId);
        Guid unitId = await SeedUnitOfMeasureAsync();
        Guid materialId = await SeedMaterialAsync(unitId, MaterialKind.Durable, TrackingType.Quantity);
        Guid employeeId = await SeedEmployeeAsync(siteId);
        Guid issueDocId = await SeedPostedIssueDocumentAsync(warehouseId, userId);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DurableCustodyAllocation alloc = DurableCustodyAllocation.Open(
                Guid.NewGuid(),
                materialId,
                warehouseId,
                PartyType.Employee,
                employeeId,
                CustodyKind.Personal,
                issueDocId,
                8m,
                DateTime.UtcNow).Value;

            dbContext.DurableCustodyAllocations.Add(alloc);
            await dbContext.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"returns/eligible-items?originalIssueDocumentId={issueDocId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiResponse<IReadOnlyList<ReturnEligibleItemResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<ReturnEligibleItemResponse>>>();
        envelope.ShouldNotBeNull();
        envelope.Data.ShouldNotBeNull();
        envelope.Data.ShouldContain(i => i.MaterialId == materialId && i.AvailableQuantity == 8m);
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        UserRoleScope? assignment = await dbContext.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);

        if (assignment is null)
        {
            Role adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "Administrator");
            dbContext.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                adminRole.Id,
                ScopeType.Enterprise,
                null));

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<Guid> SeedSiteAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..10];
        var org = Organization.Create(Guid.NewGuid(), $"Org {suffix}", $"O{suffix}");
        dbContext.Organizations.Add(org);

        var site = Site.Create(Guid.NewGuid(), org.Id, $"Site {suffix}", $"S{suffix}", "Address");
        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync();
        return site.Id;
    }

    private async Task<Guid> SeedWarehouseAsync(Guid siteId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..10];
        var warehouse = Warehouse.Create(
            Guid.NewGuid(),
            siteId,
            $"Warehouse {suffix}",
            $"W{suffix}",
            "General",
            true,
            null);

        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();
        return warehouse.Id;
    }

    private async Task<Guid> SeedUnitOfMeasureAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..6];
        var unit = UnitOfMeasure.Create(
            Guid.NewGuid(),
            $"Piece {suffix}",
            $"PCS{suffix}",
            "Pcs");

        dbContext.UnitsOfMeasure.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit.Id;
    }

    private async Task<Guid> SeedMaterialAsync(Guid unitId, MaterialKind kind, TrackingType tracking)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        var cat = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, $"Cat {suffix}", $"C{suffix}");
        var family = MaterialFamily.Create(Guid.NewGuid(), cat.Id, $"Fam {suffix}", $"F{suffix}", unitId);

        dbContext.MaterialDomains.Add(domain);
        dbContext.MaterialCategories.Add(cat);
        dbContext.MaterialFamilies.Add(family);

        var material = Material.Create(
            Guid.NewGuid(),
            family.Id,
            unitId,
            "أداة معمرة",
            "Durable Tool",
            $"MAT-P8-{suffix}",
            kind,
            tracking,
            false,
            null);

        dbContext.Materials.Add(material);
        await dbContext.SaveChangesAsync();
        return material.Id;
    }

    private async Task<Guid> SeedEmployeeAsync(Guid siteId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        var orgUnit = OrganizationalUnit.Create(
            Guid.NewGuid(),
            siteId,
            null,
            $"Dept {suffix}",
            "Department");
        dbContext.OrganizationalUnits.Add(orgUnit);

        var employee = Employee.Create(
            Guid.NewGuid(),
            orgUnit.Id,
            $"Employee {suffix}",
            $"EMP-{suffix}",
            "Technician");

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<Guid> SeedPostedIssueDocumentAsync(Guid warehouseId, Guid userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var doc = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            warehouseId,
            DocumentType.Issue,
            $"ISSUE-P8-{Guid.NewGuid():N}"[..15]);

        doc.UpdatePaperReference("P-P8-001", 2026);
        dbContext.WarehouseDocuments.Add(doc);
        await dbContext.SaveChangesAsync();

        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(),
            doc.Id,
            AttachmentType.SignedOriginal,
            $"/storage/issue-{doc.Id}.pdf",
            "signed-issue.pdf",
            "application/pdf",
            1024,
            "hash-issue-123",
            userId,
            DateTime.UtcNow);

        dbContext.DocumentAttachments.Add(attachment);
        doc.SetSignedCopy(attachment.Id);
        doc.Submit();
        await dbContext.SaveChangesAsync();

        doc.MarkPosted(userId, DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        return doc.Id;
    }
}
