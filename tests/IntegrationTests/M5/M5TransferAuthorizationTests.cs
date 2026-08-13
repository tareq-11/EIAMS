using System.Net;
using System.Net.Http.Json;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.TransferInfos;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M5;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M5TransferAuthorizationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M5TransferAuthorizationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task TransferPost_Should_ReturnNotFoundAndRemainSubmitted_WhenReviewIsMissingAtDestination()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        SubmittedTransfer transfer = await SeedSubmittedTransferAsync(userId);
        await GrantSourceReviewOnlyAsync(userId, transfer.SourceWarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{transfer.DocumentId}/post",
            new { expectedRowVersion = transfer.RowVersion });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == transfer.DocumentId)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == transfer.DocumentId)).ShouldBeFalse();
    }

    private async Task<SubmittedTransfer> SeedSubmittedTransferAsync(Guid createdBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var source = Warehouse.Create(Guid.NewGuid(), site.Id, $"Source {suffix}", $"WS{suffix}", "Main", true);
        var destination = Warehouse.Create(Guid.NewGuid(), site.Id, $"Destination {suffix}", $"WD{suffix}", "Main", true);
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Piece {suffix}", $"P{suffix}", "Count");
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        var category = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, $"Category {suffix}", $"C{suffix}");
        var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, $"Family {suffix}", $"F{suffix}", unit.Id);
        var material = Material.Create(
            Guid.NewGuid(), family.Id, $"Material {suffix}", null, $"M{suffix}", MaterialKind.Consumable,
            TrackingType.Quantity, false, false, null);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), source.Id, DocumentType.Transfer, $"TR-{suffix}");
        Result<DocumentLine> line = DocumentLine.Create(
            Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Normal, 1m, unit.Id, 1m,
            null, null, null, null);
        line.IsSuccess.ShouldBeTrue();
        Result<TransferInfo> transferInfo = TransferInfo.Create(document.Id, destination.Id, "Replenishment");
        transferInfo.IsSuccess.ShouldBeTrue();
        dbContext.AddRange(
            organization,
            site,
            source,
            destination,
            unit,
            domain,
            category,
            family,
            material,
            document,
            line.Value,
            transferInfo.Value);
        await dbContext.SaveChangesAsync();

        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"m5/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, createdBy, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();

        return new SubmittedTransfer(document.Id, document.RowVersion, source.Id);
    }

    private async Task GrantSourceReviewOnlyAsync(Guid userId, Guid sourceWarehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = Role.Create(Guid.NewGuid(), $"M5 transfer review {Guid.NewGuid():N}", null);
        dbContext.Roles.Add(role);
        dbContext.RolePermissions.Add(RolePermission.Create(role.Id, WellKnownPermissions.WarehouseDocumentsReviewId));
        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, role.Id, ScopeType.Warehouse, sourceWarehouseId));
        await dbContext.SaveChangesAsync();
    }

    private sealed record SubmittedTransfer(Guid DocumentId, int RowVersion, Guid SourceWarehouseId);
}
