using System.Net;
using System.Text.Json;
using Application.Abstractions.Posting;
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
using Domain.ReceivingInfos;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M4;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReversalIdempotencyApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ReversalIdempotencyApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ConcurrentCreateReversalWithSameIdempotencyKey_ShouldCreateAndReturnOneReversal()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        (Guid sourceDocumentId, Guid warehouseId) = await SeedAndPostReceivingDocumentAsync(userId);
        await GrantReversalPermissionAsync(userId, warehouseId);
        AccessTokens tokens = await LoginAsync(UsernameFor(email));
        Authenticate(tokens.AccessToken);
        var key = Guid.NewGuid();

        Task<HttpResponseMessage> firstTask = SendReversalAsync(sourceDocumentId, key);
        Task<HttpResponseMessage> secondTask = SendReversalAsync(sourceDocumentId, key);
        HttpResponseMessage[] responses = await Task.WhenAll(firstTask, secondTask);
        using HttpResponseMessage firstResponse = responses[0];
        using HttpResponseMessage secondResponse = responses[1];

        string[] responseBodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));
        responses.Select(response => response.StatusCode)
            .ShouldAllBe(status => status == HttpStatusCode.Created, string.Join(" | ", responseBodies));
        Guid[] reversalIds = await Task.WhenAll(responses.Select(async response =>
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        }));
        reversalIds.Distinct().Count().ShouldBe(1);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument reversal = await context.WarehouseDocuments.SingleAsync(document =>
            document.ReversalOfDocumentId == sourceDocumentId);
        reversal.Id.ShouldBe(reversalIds[0]);
        reversal.ReferenceSiteId.ShouldNotBeNull();
        reversal.ReferenceYear.ShouldBe(DateTime.UtcNow.Year);
        reversal.ReferenceSequence.ShouldNotBeNull();
        reversal.ReferenceSequence.Value.ShouldBeGreaterThan(0);
        (await context.IdempotencyRecords.CountAsync(record => record.Key == key)).ShouldBe(1);
    }

    private async Task GrantReversalPermissionAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"4D reversal {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentReverseId));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateRequest(Guid sourceDocumentId, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"warehouse-documents/{sourceDocumentId}/reversals");
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return request;
    }

    private async Task<HttpResponseMessage> SendReversalAsync(Guid sourceDocumentId, Guid key)
    {
        using HttpRequestMessage request = CreateRequest(sourceDocumentId, key);
        return await HttpClient.SendAsync(request);
    }

    private async Task<(Guid DocumentId, Guid WarehouseId)> SeedAndPostReceivingDocumentAsync(Guid userId)
    {
        Guid documentId;
        Guid warehouseId;
        int rowVersion;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..10];
            var organization = Organization.Create(Guid.NewGuid(), $"4D Org {suffix}", $"4DO{suffix}");
            var site = Site.Create(Guid.NewGuid(), organization.Id, $"4D Site {suffix}", $"4DS{suffix}", null);
            var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"4D Warehouse {suffix}", $"4DW{suffix}", "Main", true);
            var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"4D Unit {suffix}", $"4DU{suffix}", "Count");
            var domain = MaterialDomain.Create(Guid.NewGuid(), $"4D Domain {suffix}", $"4DD{suffix}");
            var category = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, $"4D Category {suffix}", $"4DC{suffix}");
            var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, $"4D Family {suffix}", $"4DF{suffix}");
            var material = Material.Create(Guid.NewGuid(), family.Id, unit.Id, $"4D Material {suffix}", null, $"4DM{suffix}",
                MaterialKind.Consumable, TrackingType.Quantity, false, null);
            var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"4D-RCV-{suffix}");
            var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouse.Id, domain.Id);
            Result<DocumentLine> lineResult = DocumentLine.Create(
                Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Normal, 1m, unit.Id, 1m,
                null, null, null,
                provenance: DocumentLineProvenance.Create(
                    material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value).Value;
            lineResult.IsSuccess.ShouldBeTrue();
            ReceivingInfo receiving = ReceivingInfo.Create(document.Id, $"Supplier {suffix}", null, ReceivingType.Supplier).Value;
            context.AddRange(organization, site, warehouse, unit, domain, category, family, material,
                WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Receiving),
                capability, document, lineResult.Value, receiving);
            await context.SaveChangesAsync();

            context.DocumentAttachments.Add(DocumentAttachment.Create(
                Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"4d/{suffix}.pdf", $"{suffix}.pdf",
                "application/pdf", 10, suffix, userId, DateTime.UtcNow));
            await context.SaveChangesAsync();
            Guid attachmentId = await context.DocumentAttachments.Where(item => item.DocumentId == document.Id)
                .Select(item => item.Id).SingleAsync();
            document.SetSignedCopy(attachmentId).IsSuccess.ShouldBeTrue();
            document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
            document.Submit().IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync();
            documentId = document.Id;
            warehouseId = warehouse.Id;
            rowVersion = document.RowVersion;
        }

        await using AsyncServiceScope postingScope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = postingScope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> postResult = await coordinator.PostAsync(documentId, rowVersion, userId, CancellationToken.None);
        postResult.IsSuccess.ShouldBeTrue(postResult.IsFailure ? postResult.Error.ToString() : "");
        return (documentId, warehouseId);
    }
}
