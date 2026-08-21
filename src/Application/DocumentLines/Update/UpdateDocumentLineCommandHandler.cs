using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.DocumentLines.Update;

internal sealed class UpdateDocumentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IOptions<AssetCreationOptions> assetCreationOptions)
    : ICommandHandler<UpdateDocumentLineCommand>
{
    public async Task<Result> Handle(UpdateDocumentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(command.DocumentId, document.DocumentStatus));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Failure(WarehouseDocumentErrors.ReversalLinesImmutable(document.Id));
        }

        DocumentLine? line = await context.DocumentLines.SingleOrDefaultAsync(
            l => l.Id == command.LineId && l.DocumentId == command.DocumentId,
            cancellationToken);

        if (line is null)
        {
            return Result.Failure(DocumentLineErrors.NotFound(command.LineId));
        }

        bool hasAdjustmentDetail = await context.AdjustmentLines.AsNoTracking()
            .AnyAsync(item => item.Id == line.Id, cancellationToken);
        if (hasAdjustmentDetail)
        {
            return Result.Failure(AdjustmentLineErrors.MustUseDedicatedEndpoint(line.Id));
        }

        Result<DocumentLineCatalogContext> catalogResult = await DocumentLineCatalogResolver.ResolveAsync(
            context,
            line.MaterialId,
            command.UnitId,
            cancellationToken);

        if (catalogResult.IsFailure)
        {
            return Result.Failure(catalogResult.Error);
        }

        DocumentLineCatalogContext catalog = catalogResult.Value;

        Result openingTypeResult = OpeningLineRules.Validate(
            document.DocumentType,
            document.Id,
            command.OpeningType);

        if (openingTypeResult.IsFailure)
        {
            return openingTypeResult;
        }

        Result<decimal> baseQuantityResult = BaseQuantityCalculator.Calculate(
            line.MaterialId,
            command.Quantity,
            command.UnitId,
            catalog.Family.BaseUnitId,
            catalog.Conversion);

        if (baseQuantityResult.IsFailure)
        {
            return Result.Failure(baseQuantityResult.Error);
        }

        DocumentLineType expectedLineType = catalog.Material.IsAssetTracked
            ? DocumentLineType.Asset
            : DocumentLineType.Normal;

        Result assetQuantityResult = AssetLineRules.Validate(
            line.Id,
            expectedLineType,
            baseQuantityResult.Value,
            assetCreationOptions.Value.MaxAssetsPerLine);

        if (assetQuantityResult.IsFailure)
        {
            return assetQuantityResult;
        }

        int lineCount = await context.DocumentLines
            .AsNoTracking()
            .CountAsync(documentLine => documentLine.DocumentId == document.Id, cancellationToken);
        decimal otherAssetQuantity = await context.DocumentLines
            .AsNoTracking()
            .Where(documentLine =>
                documentLine.DocumentId == document.Id &&
                documentLine.Id != line.Id &&
                documentLine.LineType == DocumentLineType.Asset)
            .SumAsync(documentLine => documentLine.BaseQuantity, cancellationToken);

        Result documentLimitResult = DocumentAssetLimitRules.Validate(
            document.Id,
            lineCount,
            otherAssetQuantity + (expectedLineType == DocumentLineType.Asset ? baseQuantityResult.Value : 0m),
            assetCreationOptions.Value);

        if (documentLimitResult.IsFailure)
        {
            return documentLimitResult;
        }

        bool hasChanges = line.LineType != expectedLineType ||
            line.Quantity != command.Quantity ||
            line.UnitId != command.UnitId ||
            line.BaseQuantity != baseQuantityResult.Value ||
            line.UnitPrice != command.UnitPrice ||
            line.BatchNumber != command.BatchNumber ||
            line.ExpiryDate != command.ExpiryDate ||
            line.OpeningType != command.OpeningType;

        if (!hasChanges)
        {
            return Result.Success();
        }

        Result updateResult = line.Update(
            expectedLineType,
            command.Quantity,
            command.UnitId,
            baseQuantityResult.Value,
            command.UnitPrice,
            command.BatchNumber,
            command.ExpiryDate,
            command.OpeningType);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        Result detailMutationResult = document.RegisterDetailMutation();

        if (detailMutationResult.IsFailure)
        {
            return detailMutationResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(d => d.Id == command.DocumentId)
                .Select(d => (int?)d.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        return Result.Success();
    }
}
