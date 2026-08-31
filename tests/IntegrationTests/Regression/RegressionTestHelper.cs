using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.Employees;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.IssueTos;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.ReceivingInfos;
using Domain.ReturnInfos;
using Domain.Roles;
using Domain.Sites;
using Domain.TransferInfos;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

public sealed record RegressionSeedData(
    Guid OrganizationId,
    Guid SiteId,
    Guid WarehouseId,
    Guid DestinationWarehouseId,
    Guid OrgUnitId,
    Guid EmployeeId,
    Guid KeeperUserId,
    Guid ManagerUserId,
    Guid AdminUserId,
    Guid UnitOfMeasureId,
    Guid DomainId,
    Guid CategoryId,
    Guid FamilyId,
    Guid NormalMaterialId,
    Guid AssetMaterialId);

public static class RegressionTestHelper
{
    public static async Task<RegressionSeedData> SeedAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var org = Organization.Create(Guid.NewGuid(), $"Regression Org {Guid.NewGuid():N}", $"ORG-{Guid.NewGuid():N}"[..12]);
        context.Organizations.Add(org);

        var site = Site.Create(Guid.NewGuid(), org.Id, $"Regression Site {Guid.NewGuid():N}", $"SITE-{Guid.NewGuid():N}"[..12], "Test Location");
        context.Sites.Add(site);

        var orgUnit = OrganizationalUnit.Create(Guid.NewGuid(), site.Id, null, "IT Department", "Department");
        context.OrganizationalUnits.Add(orgUnit);

        var wh1 = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse 1 {Guid.NewGuid():N}",
            $"WH1-{Guid.NewGuid():N}"[..12], "Central", true, orgUnit.Id);
        var wh2 = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse 2 {Guid.NewGuid():N}",
            $"WH2-{Guid.NewGuid():N}"[..12], "Branch", true, orgUnit.Id);
        context.Warehouses.AddRange(wh1, wh2);

        var emp = Employee.Create(Guid.NewGuid(), orgUnit.Id, "John Doe", $"EMP-{Guid.NewGuid():N}"[..12], "Engineer");
        context.Employees.Add(emp);

        var uom = UnitOfMeasure.Create(Guid.NewGuid(), "Piece", "pcs", "Quantity");
        context.UnitsOfMeasure.Add(uom);

        var domain = MaterialDomain.Create(Guid.NewGuid(), "IT Equipment", $"DOM-{Guid.NewGuid():N}"[..12]);
        context.MaterialDomains.Add(domain);

        var category = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, "Hardware", $"CAT-{Guid.NewGuid():N}"[..12]);
        context.MaterialCategories.Add(category);

        var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, "Laptops", $"FAM-{Guid.NewGuid():N}"[..12], uom.Id);
        context.MaterialFamilies.Add(family);

        var normalMat = Material.Create(
            Guid.NewGuid(),
            family.Id,
            uom.Id,
            "Mouse Pad",
            "Mouse Pad EN",
            $"MAT-N-{Guid.NewGuid():N}"[..12],
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null);

        var assetMat = Material.Create(
            Guid.NewGuid(),
            family.Id,
            uom.Id,
            "Laptop Core i7",
            "Laptop Core i7 EN",
            $"MAT-A-{Guid.NewGuid():N}"[..12],
            MaterialKind.Asset,
            TrackingType.Serial,
            false,
            null);

        context.Materials.AddRange(normalMat, assetMat);

        // Setup capabilities for wh1 and wh2
        var cap1 = WarehouseCapability.Create(Guid.NewGuid(), wh1.Id, domain.Id);
        var cap2 = WarehouseCapability.Create(Guid.NewGuid(), wh2.Id, domain.Id);
        context.WarehouseCapabilities.AddRange(cap1, cap2);

        foreach (OperationType op in new[]
                 {
                     OperationType.Receiving,
                     OperationType.Issue,
                     OperationType.Transfer,
                     OperationType.Count,
                     OperationType.Return,
                     OperationType.Adjustment
                 })
        {
            context.WarehouseCapabilityOperations.Add(WarehouseCapabilityOperation.Create(Guid.NewGuid(), cap1.Id, op));
            context.WarehouseCapabilityOperations.Add(WarehouseCapabilityOperation.Create(Guid.NewGuid(), cap2.Id, op));
        }

        // Setup users
        var keeperUser = User.Create(Guid.NewGuid(), $"keeper-{Guid.NewGuid():N}@test.com", "Keeper", "User", "hash");
        keeperUser.LinkToEmployee(emp.Id);
        var managerUser = User.Create(Guid.NewGuid(), $"mgr-{Guid.NewGuid():N}@test.com", "Manager", "User", "hash");
        var adminUser = User.Create(Guid.NewGuid(), $"admin-{Guid.NewGuid():N}@test.com", "Admin", "User", "hash");
        context.Users.AddRange(keeperUser, managerUser, adminUser);

        // Setup roles & scopes
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            keeperUser.Id,
            WellKnownRoles.WarehouseKeeperId,
            ScopeType.Warehouse,
            wh1.Id));

        // One hierarchical assignment covers both warehouses owned by this organizational unit.
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            managerUser.Id,
            WellKnownRoles.WarehouseManagerId,
            ScopeType.OrganizationalUnit,
            orgUnit.Id));

        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), adminUser.Id, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));

        await context.SaveChangesAsync();

        return new RegressionSeedData(
            org.Id,
            site.Id,
            wh1.Id,
            wh2.Id,
            orgUnit.Id,
            emp.Id,
            keeperUser.Id,
            managerUser.Id,
            adminUser.Id,
            uom.Id,
            domain.Id,
            category.Id,
            family.Id,
            normalMat.Id,
            assetMat.Id);
    }

    public static async Task<WarehouseDocument> CreateAndSubmitDocumentAsync(
        IServiceProvider serviceProvider,
        Guid warehouseId,
        DocumentType documentType,
        Guid createdBy,
        IReadOnlyList<(Guid MaterialId, DocumentLineType LineType, decimal Quantity)> lines,
        Action<WarehouseDocument, ApplicationDbContext>? petalSetup = null)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var doc = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            warehouseId,
            documentType,
            $"SYS-{Guid.NewGuid():N}"[..15]);

        doc.UpdatePaperReference($"PAPER-{Guid.NewGuid():N}"[..10], DateTime.UtcNow.Year);

        context.WarehouseDocuments.Add(doc);

        foreach ((Guid MaterialId, DocumentLineType LineType, decimal Quantity) line in lines)
        {
            Result<DocumentLine> docLineResult = DocumentLine.Create(
                Guid.NewGuid(),
                doc.Id,
                line.MaterialId,
                line.LineType,
                line.Quantity,
                null,
                line.Quantity,
                null,
                null,
                null,
                documentType == DocumentType.Opening ? OpeningType.Initial : null);

            context.DocumentLines.Add(docLineResult.Value);
        }

        petalSetup?.Invoke(doc, context);

        if (documentType == DocumentType.Receiving &&
            !context.ReceivingInfos.Local.Any(info => info.Id == doc.Id))
        {
            context.ReceivingInfos.Add(
                ReceivingInfo.Create(doc.Id, "Regression supplier", null, ReceivingType.Supplier).Value);
        }

        // Persist the document before linking its signed attachment. The attachment points to the
        // document while the document points back to the selected attachment, so inserting both
        // ends in one batch creates an unresolvable FK dependency cycle for EF Core.
        await context.SaveChangesAsync();

        var signedAttachment = DocumentAttachment.Create(
            Guid.NewGuid(),
            doc.Id,
            AttachmentType.SignedOriginal,
            $"/storage/{Guid.NewGuid():N}.pdf",
            $"signed-{Guid.NewGuid():N}.pdf",
            "application/pdf",
            1024,
            "hash123",
            createdBy,
            DateTime.UtcNow);

        context.DocumentAttachments.Add(signedAttachment);
        doc.SetSignedCopy(signedAttachment.Id);
        doc.Submit();

        await context.SaveChangesAsync();
        return doc;
    }
}
