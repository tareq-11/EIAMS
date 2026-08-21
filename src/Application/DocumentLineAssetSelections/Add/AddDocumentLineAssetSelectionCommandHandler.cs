using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.ReturnInfos;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLineAssetSelections.Add;

internal sealed class AddDocumentLineAssetSelectionCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<AddDocumentLineAssetSelectionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        AddDocumentLineAssetSelectionCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        Result guardResult = await GuardDocumentAsync(document, command, cancellationToken);

        if (guardResult.IsFailure)
        {
            return Result.Failure<Guid>(guardResult.Error);
        }

        DocumentLine? line = await context.DocumentLines
            .SingleOrDefaultAsync(
                item => item.Id == command.LineId && item.DocumentId == command.DocumentId,
                cancellationToken);

        if (line is null)
        {
            return Result.Failure<Guid>(DocumentLineErrors.NotFound(command.LineId));
        }

        if (line.LineType != DocumentLineType.Asset)
        {
            return Result.Failure<Guid>(DocumentLineAssetSelectionErrors.UnsupportedLineType(line.Id));
        }

        Asset? asset = await context.Assets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.AssetId, cancellationToken);

        if (asset is null)
        {
            return Result.Failure<Guid>(AssetErrors.NotFound(command.AssetId));
        }

        if (asset.MaterialId != line.MaterialId)
        {
            return Result.Failure<Guid>(
                DocumentLineAssetSelectionErrors.AssetNotForLineMaterial(asset.Id, line.Id));
        }

        AssetCurrentStatus? terminalStatus = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => item.AssetId == asset.Id)
            .Select(item => (AssetCurrentStatus?)item.CurrentStatus)
            .SingleOrDefaultAsync(cancellationToken);
        if (terminalStatus == AssetCurrentStatus.Disposed)
        {
            return Result.Failure<Guid>(DisposalErrors.AssetAlreadyDisposed(asset.Id));
        }

        Result eligibilityResult = document.DocumentType switch
        {
            DocumentType.Issue => await ValidateIssueAssetAsync(document, asset, cancellationToken),
            DocumentType.Return => await ValidateReturnAssetAsync(document, asset, cancellationToken),
            DocumentType.Adjustment => await ValidateDisposalAssetAsync(document, line, asset, cancellationToken),
            _ => Result.Failure(DocumentLineAssetSelectionErrors.UnsupportedDocumentType(document.Id))
        };

        if (eligibilityResult.IsFailure)
        {
            return Result.Failure<Guid>(eligibilityResult.Error);
        }

        bool duplicate = await context.DocumentLineAssetSelections.AnyAsync(
            item => item.DocumentId == command.DocumentId && item.AssetId == command.AssetId,
            cancellationToken);

        if (duplicate)
        {
            return Result.Failure<Guid>(
                DocumentLineAssetSelectionErrors.Duplicate(command.DocumentId, command.AssetId));
        }

        var selectionId = Guid.NewGuid();
        Result<DocumentLineAssetSelection> createResult = DocumentLineAssetSelection.Create(
            selectionId,
            command.DocumentId,
            command.LineId,
            command.AssetId);

        if (createResult.IsFailure)
        {
            return Result.Failure<Guid>(createResult.Error);
        }

        context.DocumentLineAssetSelections.Add(createResult.Value);
        Result mutationResult = document.RegisterDetailMutation();

        if (mutationResult.IsFailure)
        {
            return Result.Failure<Guid>(mutationResult.Error);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<Guid>(await GetRowVersionErrorAsync(command, cancellationToken));
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(
                DocumentLineAssetSelectionErrors.Duplicate(command.DocumentId, command.AssetId));
        }

        return selectionId;
    }

    private async Task<Result> GuardDocumentAsync(
        WarehouseDocument document,
        AddDocumentLineAssetSelectionCommand command,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(document.Id));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                document.Id,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(document.Id, document.DocumentStatus));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Failure(WarehouseDocumentErrors.ReversalLinesImmutable(document.Id));
        }

        if (document.DocumentType is DocumentType.Issue or DocumentType.Return)
        {
            return Result.Success();
        }

        if (document.DocumentType == DocumentType.Adjustment)
        {
            AdjustmentKind? kind = await context.InventoryAdjustments.AsNoTracking()
                .Where(item => item.Id == document.Id)
                .Select(item => (AdjustmentKind?)item.AdjustmentKind)
                .SingleOrDefaultAsync(cancellationToken);
            return kind == AdjustmentKind.Disposal
                ? Result.Success()
                : Result.Failure(DocumentLineAssetSelectionErrors.UnsupportedDocumentType(document.Id));
        }

        return Result.Failure(DocumentLineAssetSelectionErrors.UnsupportedDocumentType(document.Id));
    }

    private async Task<Result> ValidateIssueAssetAsync(
        WarehouseDocument document,
        Asset asset,
        CancellationToken cancellationToken)
    {
        if (asset.WarehouseId != document.WarehouseId)
        {
            return Result.Failure(
                DocumentLineAssetSelectionErrors.AssetNotInSourceWarehouse(asset.Id, document.WarehouseId));
        }

        AssetCurrentStatus? status = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => item.AssetId == asset.Id)
            .Select(item => (AssetCurrentStatus?)item.CurrentStatus)
            .SingleOrDefaultAsync(cancellationToken);

        return status == AssetCurrentStatus.InStock
            ? Result.Success()
            : Result.Failure(DocumentLineAssetSelectionErrors.AssetNotInStock(asset.Id));
    }

    private async Task<Result> ValidateReturnAssetAsync(
        WarehouseDocument document,
        Asset asset,
        CancellationToken cancellationToken)
    {
        ReturnInfo? info = await context.ReturnInfos.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        if (info is null)
        {
            return Result.Failure(ReturnInfoErrors.Required(document.Id));
        }

        bool matchingCustody = await context.Custodies.AsNoTracking().AnyAsync(
            item => item.AssetId == asset.Id &&
                item.Status == CustodyStatus.Active &&
                item.IssueDocumentId == info.OriginalIssueDocumentId,
            cancellationToken);

        return matchingCustody
            ? Result.Success()
            : Result.Failure(DocumentLineAssetSelectionErrors.ActiveCustodyMismatch(
                asset.Id,
                info.OriginalIssueDocumentId));
    }

    private async Task<Result> ValidateDisposalAssetAsync(
        WarehouseDocument document,
        DocumentLine line,
        Asset asset,
        CancellationToken cancellationToken)
    {
        if (asset.WarehouseId != document.WarehouseId)
        {
            return Result.Failure(
                DocumentLineAssetSelectionErrors.AssetNotInSourceWarehouse(asset.Id, document.WarehouseId));
        }

        AssetCurrentStatus? status = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => item.AssetId == asset.Id)
            .Select(item => (AssetCurrentStatus?)item.CurrentStatus)
            .SingleOrDefaultAsync(cancellationToken);
        if (status is not (AssetCurrentStatus.InStock or AssetCurrentStatus.Issued or AssetCurrentStatus.InCustody))
        {
            return Result.Failure(DisposalErrors.UnsupportedState(asset.Id));
        }

        decimal? difference = await context.AdjustmentLines.AsNoTracking()
            .Where(item => item.Id == line.Id && item.AdjustmentId == document.Id)
            .Select(item => (decimal?)item.Difference)
            .SingleOrDefaultAsync(cancellationToken);
        decimal expected = status == AssetCurrentStatus.InStock ? -1m : 0m;
        return difference == expected
            ? Result.Success()
            : Result.Failure(DisposalErrors.AssetStateChanged(asset.Id));
    }

    private async Task<Error> GetRowVersionErrorAsync(
        AddDocumentLineAssetSelectionCommand command,
        CancellationToken cancellationToken)
    {
        int? current = await context.WarehouseDocuments.AsNoTracking()
            .Where(item => item.Id == command.DocumentId)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);

        return WarehouseDocumentErrors.RowVersionMismatch(
            command.DocumentId,
            command.ExpectedRowVersion,
            current);
    }
}
