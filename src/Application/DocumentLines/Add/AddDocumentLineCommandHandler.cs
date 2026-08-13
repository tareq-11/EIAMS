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
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.DocumentLines.Add;

internal sealed class AddDocumentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IOptions<AssetCreationOptions> assetCreationOptions)
    : ICommandHandler<AddDocumentLineCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddDocumentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotEditable(command.DocumentId, document.DocumentStatus));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.ReversalLinesImmutable(document.Id));
        }

        Result<DocumentLineCatalogContext> catalogResult = await DocumentLineCatalogResolver.ResolveAsync(
            context,
            command.MaterialId,
            command.UnitId,
            cancellationToken);

        if (catalogResult.IsFailure)
        {
            return Result.Failure<Guid>(catalogResult.Error);
        }

        DocumentLineCatalogContext catalog = catalogResult.Value;

        Result openingTypeResult = OpeningLineRules.Validate(
            document.DocumentType,
            document.Id,
            command.OpeningType);

        if (openingTypeResult.IsFailure)
        {
            return Result.Failure<Guid>(openingTypeResult.Error);
        }

        Result<decimal> baseQuantityResult = BaseQuantityCalculator.Calculate(
            command.MaterialId,
            command.Quantity,
            command.UnitId,
            catalog.Family.BaseUnitId,
            catalog.Conversion);

        if (baseQuantityResult.IsFailure)
        {
            return Result.Failure<Guid>(baseQuantityResult.Error);
        }

        DocumentLineType lineType = catalog.Material.IsAssetTracked
            ? DocumentLineType.Asset
            : DocumentLineType.Normal;

        var lineId = Guid.NewGuid();

        Result assetQuantityResult = AssetLineRules.Validate(
            lineId,
            lineType,
            baseQuantityResult.Value,
            assetCreationOptions.Value.MaxAssetsPerLine);

        if (assetQuantityResult.IsFailure)
        {
            return Result.Failure<Guid>(assetQuantityResult.Error);
        }

        int existingLineCount = await context.DocumentLines
            .AsNoTracking()
            .CountAsync(line => line.DocumentId == document.Id, cancellationToken);
        decimal existingAssetQuantity = await context.DocumentLines
            .AsNoTracking()
            .Where(line => line.DocumentId == document.Id && line.LineType == DocumentLineType.Asset)
            .SumAsync(line => line.BaseQuantity, cancellationToken);

        Result documentLimitResult = DocumentAssetLimitRules.Validate(
            document.Id,
            existingLineCount + 1,
            existingAssetQuantity + (lineType == DocumentLineType.Asset ? baseQuantityResult.Value : 0m),
            assetCreationOptions.Value);

        if (documentLimitResult.IsFailure)
        {
            return Result.Failure<Guid>(documentLimitResult.Error);
        }

        Result<DocumentLine> lineResult = DocumentLine.Create(
            lineId,
            command.DocumentId,
            command.MaterialId,
            lineType,
            command.Quantity,
            command.UnitId,
            baseQuantityResult.Value,
            command.UnitPrice,
            command.BatchNumber,
            command.ExpiryDate,
            command.OpeningType);

        if (lineResult.IsFailure)
        {
            return Result.Failure<Guid>(lineResult.Error);
        }

        context.DocumentLines.Add(lineResult.Value);

        Result detailMutationResult = document.RegisterDetailMutation();

        if (detailMutationResult.IsFailure)
        {
            return Result.Failure<Guid>(detailMutationResult.Error);
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

            return Result.Failure<Guid>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        return lineResult.Value.Id;
    }
}
