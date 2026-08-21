using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.Employees;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.OrganizationalUnits;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.M6;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M6DatabaseAndApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M6DatabaseAndApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("assets/00000000-0000-0000-0000-000000000001/current-status")]
    [InlineData("assets/00000000-0000-0000-0000-000000000001/custody-timeline")]
    [InlineData("custodies/pending?warehouseId=00000000-0000-0000-0000-000000000001")]
    public async Task AssetAndCustodyReadRoutes_Should_ReturnUnauthorized_WhenTokenIsMissing(string route)
    {
        // Arrange

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task M6WriteRoutes_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        HttpResponseMessage selectionResponse = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{documentId}/lines/{lineId}/assets",
            new { assetId, expectedRowVersion = 1 });
        HttpResponseMessage returnInfoResponse = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/return-info",
            new { originalIssueDocumentId = Guid.NewGuid(), returnReason = "Return", expectedRowVersion = 1 });
        HttpResponseMessage assignmentResponse = await HttpClient.PostAsJsonAsync(
            $"assets/{assetId}/custody-assignment",
            new { employeeId = Guid.NewGuid(), expectedCustodyRowVersion = 1, note = "Assignment" });

        // Assert
        selectionResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        returnInfoResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentStatus_Should_HideAsset_WhenUserHasPermissionInAnotherWarehouse()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.OtherWarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"assets/{seed.AssetId}/current-status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAssetSelection_Should_HideDocument_WhenUserHasPermissionInAnotherWarehouse()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.OtherWarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/lines/{seed.DraftIssueLineId}/assets",
            new { assetId = seed.AssetId, expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertReturnInfoAndDocumentGet_Should_ReturnNormalizedDetail()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage upsertResponse = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{seed.ReturnDocumentId}/return-info",
            new
            {
                originalIssueDocumentId = seed.OriginalIssueDocumentId,
                returnReason = "  Returned in good condition  ",
                expectedRowVersion = 1
            });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{seed.ReturnDocumentId}");

        // Assert
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        M6ApiEnvelope<DocumentDetails>? body = await getResponse.Content.ReadFromJsonAsync<M6ApiEnvelope<DocumentDetails>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.ReturnInfo.ShouldNotBeNull();
        body.Data.ReturnInfo.OriginalIssueDocumentId.ShouldBe(seed.OriginalIssueDocumentId);
        body.Data.ReturnInfo.ReturnReason.ShouldBe("Returned in good condition");
    }

    [Fact]
    public async Task UpsertReturnInfo_Should_ReturnBadRequest_WhenDocumentIsNotReturn()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/return-info",
            new
            {
                originalIssueDocumentId = seed.OriginalIssueDocumentId,
                returnReason = "Wrong document type",
                expectedRowVersion = 1
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAndRemoveAssetSelectionAndDocumentGet_Should_ReflectSelectedAsset()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage addResponse = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/lines/{seed.DraftIssueLineId}/assets",
            new { assetId = seed.AssetId, expectedRowVersion = 1 });
        HttpResponseMessage getAfterAddResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}");
        HttpResponseMessage removeResponse = await HttpClient.DeleteAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/lines/{seed.DraftIssueLineId}/assets/{seed.AssetId}?expectedRowVersion=2");
        HttpResponseMessage getAfterRemoveResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}");

        // Assert
        addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        M6ApiEnvelope<DocumentDetails>? afterAdd =
            await getAfterAddResponse.Content.ReadFromJsonAsync<M6ApiEnvelope<DocumentDetails>>();
        afterAdd.ShouldNotBeNull();
        DocumentLineDetails lineAfterAdd = afterAdd.Data.Lines.Single(line => line.Id == seed.DraftIssueLineId);
        lineAfterAdd.SelectedAssets.Single().AssetId.ShouldBe(seed.AssetId);

        removeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        M6ApiEnvelope<DocumentDetails>? afterRemove =
            await getAfterRemoveResponse.Content.ReadFromJsonAsync<M6ApiEnvelope<DocumentDetails>>();
        afterRemove.ShouldNotBeNull();
        afterRemove.Data.Lines.Single(line => line.Id == seed.DraftIssueLineId).SelectedAssets.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDocumentLine_Should_ReturnDomainErrorRatherThanServerFailure_WhenLineHasAssetSelection()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        HttpResponseMessage addResponse = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/lines/{seed.DraftIssueLineId}/assets",
            new { assetId = seed.AssetId, expectedRowVersion = 1 });
        addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            $"warehouse-documents/{seed.DraftIssueDocumentId}/lines/{seed.DraftIssueLineId}?expectedRowVersion=2");

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task CurrentStatusTimelineAndPending_Should_ReturnDerivedDataWithPagination()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        await AddOperationalCustodyAsync(seed, seed.AssetId, DateTime.UtcNow.AddMinutes(-2));
        Guid secondAssetId = await AddAssetWithReceivedHistoryAsync(seed, "PENDING-SECOND");
        await AddOperationalCustodyAsync(seed, secondAssetId, DateTime.UtcNow.AddMinutes(-1));
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage statusResponse = await HttpClient.GetAsync($"assets/{seed.AssetId}/current-status");
        HttpResponseMessage timelineResponse = await HttpClient.GetAsync(
            $"assets/{seed.AssetId}/custody-timeline?page=1&pageSize=1");
        HttpResponseMessage pendingResponse = await HttpClient.GetAsync(
            $"custodies/pending?warehouseId={seed.WarehouseId}&page=1&pageSize=1");

        // Assert
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        M6ApiEnvelope<AssetStatusDetails>? status =
            await statusResponse.Content.ReadFromJsonAsync<M6ApiEnvelope<AssetStatusDetails>>();
        status.ShouldNotBeNull();
        status.Data.AssetId.ShouldBe(seed.AssetId);
        status.Data.CurrentStatus.ShouldBe("Issued");
        status.Data.CustodyKind.ShouldBe("Operational");

        timelineResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedEnvelope<CustodyTimelineDetails>? timeline =
            await timelineResponse.Content.ReadFromJsonAsync<PagedEnvelope<CustodyTimelineDetails>>();
        timeline.ShouldNotBeNull();
        timeline.Success.ShouldBeTrue();
        timeline.Data.Count.ShouldBe(1);
        timeline.Pagination.Page.ShouldBe(1);
        timeline.Pagination.PageSize.ShouldBe(1);
        timeline.Pagination.TotalItems.ShouldBe(1);

        pendingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedEnvelope<PendingCustodyDetails>? pending =
            await pendingResponse.Content.ReadFromJsonAsync<PagedEnvelope<PendingCustodyDetails>>();
        pending.ShouldNotBeNull();
        pending.Success.ShouldBeTrue();
        pending.Data.Count.ShouldBe(1);
        pending.Pagination.Page.ShouldBe(1);
        pending.Pagination.PageSize.ShouldBe(1);
        pending.Pagination.TotalItems.ShouldBe(2);
        pending.Pagination.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task CustodyConstraint_Should_RejectSecondActiveCustodyForSameAsset()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        Custody first = CreateCustody(seed.AssetId, seed.OriginalIssueDocumentId, PartyType.Site, CustodyKind.Operational);
        Custody second = CreateCustody(seed.AssetId, seed.OriginalIssueDocumentId, PartyType.Site, CustodyKind.Operational);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Custodies.Add(first);
        await context.SaveChangesAsync();
        context.Custodies.Add(second);

        // Act
        DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        PostgresException postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        postgresException.ConstraintName.ShouldBe("ix_custodies_asset_id");
    }

    [Fact]
    public async Task CustodyConstraint_Should_RejectPersonalCustodyForNonEmployee()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.custodies
                     (id, asset_id, holder_type, holder_id, custody_kind, issue_document_id,
                      status, from_utc, row_version, created_at_utc)
                 VALUES
                     ({Guid.NewGuid()}, {seed.AssetId}, {"Site"}, {Guid.NewGuid()}, {"Personal"},
                      {seed.OriginalIssueDocumentId}, {"Active"}, {DateTime.UtcNow}, {1}, {DateTime.UtcNow})
                 """));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task AssetMovementHistory_Should_RejectDirectUpdateAndDelete()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        PostgresException updateException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.asset_movement_history SET moved_at_utc = {DateTime.UtcNow} WHERE id = {seed.ReceivedHistoryId}"));
        PostgresException deleteException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.asset_movement_history WHERE id = {seed.ReceivedHistoryId}"));

        // Assert
        updateException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        deleteException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task AssetCurrentStatusView_Should_UsePrecedenceAndMovementIdTieBreak()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        Guid operationalAssetId = await AddAssetWithReceivedHistoryAsync(seed, "VIEW-OPERATIONAL");
        Guid personalAssetId = await AddAssetWithReceivedHistoryAsync(seed, "VIEW-PERSONAL");
        Guid disposedAssetId = await AddAssetWithReceivedHistoryAsync(seed, "VIEW-DISPOSED");
        Guid tieBreakAssetId = await AddAssetWithReceivedHistoryAsync(seed, "VIEW-TIE-BREAK");
        await AddOperationalCustodyAsync(seed, operationalAssetId, DateTime.UtcNow.AddMinutes(-4));
        await AddPersonalCustodyAsync(seed, personalAssetId, DateTime.UtcNow.AddMinutes(-3));
        await AddMovementAsync(seed, disposedAssetId, AssetMovementType.Disposed, Guid.Parse("00000000-0000-0000-0000-000000000001"), DateTime.UtcNow);
        DateTime tieTime = DateTime.UtcNow.AddMinutes(1);
        await AddMovementAsync(seed, tieBreakAssetId, AssetMovementType.Disposed, Guid.Parse("00000000-0000-0000-0000-000000000010"), tieTime);
        await AddMovementAsync(seed, tieBreakAssetId, AssetMovementType.Received, Guid.Parse("00000000-0000-0000-0000-000000000020"), tieTime);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        Dictionary<Guid, AssetCurrentStatus> statuses = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(view => new[] { seed.AssetId, operationalAssetId, personalAssetId, disposedAssetId, tieBreakAssetId }
                .Contains(view.AssetId))
            .ToDictionaryAsync(view => view.AssetId, view => view.CurrentStatus);

        // Assert
        statuses[seed.AssetId].ShouldBe(AssetCurrentStatus.InStock);
        statuses[operationalAssetId].ShouldBe(AssetCurrentStatus.Issued);
        statuses[personalAssetId].ShouldBe(AssetCurrentStatus.InCustody);
        statuses[disposedAssetId].ShouldBe(AssetCurrentStatus.Disposed);
        statuses[tieBreakAssetId].ShouldBe(AssetCurrentStatus.InStock);
    }

    [Fact]
    public async Task ReturnInfoConstraints_Should_RejectUnknownOriginalIssueAndBlankReason()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        PostgresException foreignKeyException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.return_info
                     (document_id, original_issue_document_id, return_reason, created_at_utc)
                 VALUES ({seed.ReturnDocumentId}, {Guid.NewGuid()}, {"Valid reason"}, {DateTime.UtcNow})
                 """));
        PostgresException checkException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.return_info
                     (document_id, original_issue_document_id, return_reason, created_at_utc)
                 VALUES ({seed.ReturnDocumentId}, {seed.OriginalIssueDocumentId}, {"   "}, {DateTime.UtcNow})
                 """));

        // Assert
        foreignKeyException.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        foreignKeyException.ConstraintName.ShouldBe("fk_return_info_warehouse_documents_original_issue_document_id");
        checkException.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        checkException.ConstraintName.ShouldBe("ck_return_info_return_reason_not_blank");
    }

    private async Task<M6Seed> SeedAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var otherWarehouseId = Guid.NewGuid();
        var organizationalUnitId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var receiptDocumentId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var originalIssueDocumentId = Guid.NewGuid();
        var returnDocumentId = Guid.NewGuid();
        var draftIssueDocumentId = Guid.NewGuid();
        var draftIssueLineId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var receivedHistoryId = Guid.NewGuid();
        var seedUserId = Guid.NewGuid();

        context.AddRange(
            Organization.Create(organizationId, $"M6 organization {suffix}", $"M6O{suffix}"),
            Site.Create(siteId, organizationId, $"M6 site {suffix}", $"M6S{suffix}", null),
            Warehouse.Create(warehouseId, siteId, $"M6 warehouse {suffix}", $"M6W{suffix}", "Main", true),
            Warehouse.Create(otherWarehouseId, siteId, $"M6 other {suffix}", $"M6X{suffix}", "Other", true),
            OrganizationalUnit.Create(organizationalUnitId, siteId, null, "M6 Operations", "Department"),
            Employee.Create(employeeId, organizationalUnitId, "M6 Employee", $"M6E{suffix}", null),
            UnitOfMeasure.Create(unitId, $"M6 piece {suffix}", $"M6P{suffix}", "Count"),
            MaterialDomain.Create(domainId, $"M6 domain {suffix}", $"M6D{suffix}"),
            MaterialCategory.Create(categoryId, domainId, null, $"M6 category {suffix}", $"M6C{suffix}"),
            MaterialFamily.Create(familyId, categoryId, $"M6 family {suffix}", $"M6F{suffix}", unitId),
            Material.Create(
                materialId,
                familyId,
                $"M6 asset material {suffix}",
                null,
                $"M6M{suffix}",
                MaterialKind.Asset,
                TrackingType.Serial,
                false,
                true,
                null),
            User.Create(seedUserId, $"m6-seed-{suffix}@example.com", "M6", "Seed", "hash"));

        var receiptDocument = WarehouseDocument.CreateDraft(
            receiptDocumentId, warehouseId, DocumentType.Receiving, $"M6-REC-{suffix}");
        Result<DocumentLine> receiptLineResult = DocumentLine.Create(
            receiptLineId, receiptDocumentId, materialId, DocumentLineType.Asset, 1m, unitId, 1m,
            null, null, null);
        receiptLineResult.IsSuccess.ShouldBeTrue();
        var originalIssue = WarehouseDocument.CreateDraft(
            originalIssueDocumentId, warehouseId, DocumentType.Issue, $"M6-ISSUE-{suffix}");
        var returnDocument = WarehouseDocument.CreateDraft(
            returnDocumentId, warehouseId, DocumentType.Return, $"M6-RETURN-{suffix}");
        var draftIssue = WarehouseDocument.CreateDraft(
            draftIssueDocumentId, warehouseId, DocumentType.Issue, $"M6-DRAFT-{suffix}");
        Result<DocumentLine> draftIssueLineResult = DocumentLine.Create(
            draftIssueLineId, draftIssueDocumentId, materialId, DocumentLineType.Asset, 1m, unitId, 1m,
            null, null, null);
        draftIssueLineResult.IsSuccess.ShouldBeTrue();
        Result<Asset> assetResult = Asset.CreateReceived(
            assetId, materialId, warehouseId, receiptLineId, $"M6-ASSET-{suffix}", DateOnly.FromDateTime(DateTime.UtcNow));
        assetResult.IsSuccess.ShouldBeTrue();
        Result<AssetMovementHistory> receivedHistoryResult = AssetMovementHistory.Create(
            receivedHistoryId, assetId, receiptDocumentId, AssetMovementType.Received, DateTime.UtcNow.AddMinutes(-10));
        receivedHistoryResult.IsSuccess.ShouldBeTrue();
        var signedOriginal = DocumentAttachment.Create(
            Guid.NewGuid(),
            originalIssueDocumentId,
            AttachmentType.SignedOriginal,
            $"m6-tests/{suffix}.pdf",
            $"{suffix}.pdf",
            "application/pdf",
            1,
            suffix,
            seedUserId,
            DateTime.UtcNow);

        context.AddRange(
            receiptDocument,
            receiptLineResult.Value,
            originalIssue,
            returnDocument,
            draftIssue,
            draftIssueLineResult.Value,
            assetResult.Value,
            receivedHistoryResult.Value,
            signedOriginal);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE public.warehouse_documents SET document_status = {"Posted"}, signed_copy_attachment_id = {signedOriginal.Id}, posted_by = {seedUserId}, posted_at_utc = {DateTime.UtcNow} WHERE id = {originalIssueDocumentId}");

        return new M6Seed(
            warehouseId,
            otherWarehouseId,
            employeeId,
            materialId,
            unitId,
            receiptDocumentId,
            receiptLineId,
            originalIssueDocumentId,
            returnDocumentId,
            draftIssueDocumentId,
            draftIssueLineId,
            assetId,
            receivedHistoryId);
    }

    private async Task<Guid> AddAssetWithReceivedHistoryAsync(M6Seed seed, string assetNumberPrefix)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assetId = Guid.NewGuid();
        Result<Asset> assetResult = Asset.CreateReceived(
            assetId,
            seed.MaterialId,
            seed.WarehouseId,
            seed.ReceiptLineId,
            $"{assetNumberPrefix}-{Guid.NewGuid():N}",
            DateOnly.FromDateTime(DateTime.UtcNow));
        assetResult.IsSuccess.ShouldBeTrue();
        Result<AssetMovementHistory> historyResult = AssetMovementHistory.Create(
            Guid.NewGuid(), assetId, seed.ReceiptDocumentId, AssetMovementType.Received, DateTime.UtcNow.AddMinutes(-10));
        historyResult.IsSuccess.ShouldBeTrue();
        context.AddRange(assetResult.Value, historyResult.Value);
        await context.SaveChangesAsync();

        return assetId;
    }

    private async Task AddMovementAsync(
        M6Seed seed,
        Guid assetId,
        AssetMovementType movementType,
        Guid movementId,
        DateTime movedAtUtc)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Result<AssetMovementHistory> result = AssetMovementHistory.Create(
            movementId, assetId, seed.OriginalIssueDocumentId, movementType, movedAtUtc);
        result.IsSuccess.ShouldBeTrue();
        context.AssetMovementHistories.Add(result.Value);
        await context.SaveChangesAsync();
    }

    private async Task AddOperationalCustodyAsync(M6Seed seed, Guid assetId, DateTime fromUtc)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Custodies.Add(CreateCustody(assetId, seed.OriginalIssueDocumentId, PartyType.Site, CustodyKind.Operational, fromUtc));
        await context.SaveChangesAsync();
    }

    private async Task AddPersonalCustodyAsync(M6Seed seed, Guid assetId, DateTime fromUtc)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Custodies.Add(CreateCustody(
            assetId, seed.OriginalIssueDocumentId, PartyType.Employee, CustodyKind.Personal, fromUtc));
        await context.SaveChangesAsync();
    }

    private static Custody CreateCustody(
        Guid assetId,
        Guid issueDocumentId,
        PartyType holderType,
        CustodyKind custodyKind,
        DateTime? fromUtc = null)
    {
        Result<Custody> result = Custody.Open(
            Guid.NewGuid(),
            assetId,
            holderType,
            Guid.NewGuid(),
            custodyKind,
            issueDocumentId,
            fromUtc ?? DateTime.UtcNow.AddMinutes(-5));
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task GrantWarehouseDocumentPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M6 role {roleId:N}", null));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsEditId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsViewId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private sealed record M6Seed(
        Guid WarehouseId,
        Guid OtherWarehouseId,
        Guid EmployeeId,
        Guid MaterialId,
        Guid UnitId,
        Guid ReceiptDocumentId,
        Guid ReceiptLineId,
        Guid OriginalIssueDocumentId,
        Guid ReturnDocumentId,
        Guid DraftIssueDocumentId,
        Guid DraftIssueLineId,
        Guid AssetId,
        Guid ReceivedHistoryId);

    private sealed record M6ApiEnvelope<T>(bool Success, T Data);
    private sealed record PagedEnvelope<T>(bool Success, List<T> Data, PaginationDetails Pagination);
    private sealed record PaginationDetails(
        int Page,
        [property: JsonPropertyName("page_size")] int PageSize,
        [property: JsonPropertyName("total_items")] int TotalItems,
        [property: JsonPropertyName("total_pages")] int TotalPages);
    private sealed record ApiErrorEnvelope(bool Success, ApiErrorDetails Error);
    private sealed record ApiErrorDetails(string Code);
    private sealed record DocumentDetails(ReturnInfoDetails? ReturnInfo, List<DocumentLineDetails> Lines);
    private sealed record ReturnInfoDetails(Guid OriginalIssueDocumentId, string ReturnReason);
    private sealed record DocumentLineDetails(Guid Id, List<SelectedAssetDetails> SelectedAssets);
    private sealed record SelectedAssetDetails(Guid AssetId);
    private sealed record AssetStatusDetails(Guid AssetId, string CurrentStatus, string? CustodyKind);
    private sealed record CustodyTimelineDetails(Guid CustodyId);
    private sealed record PendingCustodyDetails(Guid CustodyId);
}
