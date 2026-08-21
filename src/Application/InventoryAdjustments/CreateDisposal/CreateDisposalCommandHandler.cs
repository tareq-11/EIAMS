using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.WarehouseDocuments;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.CreateDisposal;

internal sealed class CreateDisposalCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IWarehouseDocumentDraftFactory draftFactory,
    IApplicationTransaction transaction,
    IAssetKeyLock assetKeyLock,
    IAssetLifecycleGuard assetLifecycleGuard,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateDisposalCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDisposalCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse, command.WarehouseId, cancellationToken);
        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.Forbidden);
        }

        return await transaction.ExecuteAsync(
            ct => CreateInTransactionAsync(command, ct), cancellationToken);
    }

    private async Task<Result<Guid>> CreateInTransactionAsync(
        CreateDisposalCommand command,
        CancellationToken cancellationToken)
    {
        Guid[] assetIds = command.AssetIds.Distinct().OrderBy(id => id).ToArray();
        await assetKeyLock.AcquireAsync(assetIds, cancellationToken);

        Result terminal = await assetLifecycleGuard.EnsureNotDisposedAsync(assetIds, cancellationToken);
        if (terminal.IsFailure)
        {
            return Result.Failure<Guid>(terminal.Error);
        }

        List<Asset> assets = await context.Assets.AsNoTracking()
            .Where(item => assetIds.Contains(item.Id) && item.WarehouseId == command.WarehouseId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (assets.Count != assetIds.Length)
        {
            Guid missing = assetIds.Except(assets.Select(item => item.Id)).First();
            return Result.Failure<Guid>(AssetErrors.NotFound(missing));
        }

        Guid? pendingAssetId = await (
                from selection in context.DocumentLineAssetSelections.AsNoTracking()
                join existingAdjustment in context.InventoryAdjustments.AsNoTracking()
                    on selection.DocumentId equals existingAdjustment.Id
                join existingDocument in context.WarehouseDocuments.AsNoTracking()
                    on existingAdjustment.Id equals existingDocument.Id
                where assetIds.Contains(selection.AssetId) &&
                    existingAdjustment.AdjustmentKind == AdjustmentKind.Disposal &&
                    (existingDocument.DocumentStatus == DocumentStatus.Draft ||
                     existingDocument.DocumentStatus == DocumentStatus.Submitted)
                select (Guid?)selection.AssetId)
            .FirstOrDefaultAsync(cancellationToken);
        if (pendingAssetId.HasValue)
        {
            return Result.Failure<Guid>(DisposalErrors.AlreadyPending(pendingAssetId.Value));
        }

        List<AssetCurrentStatusView> statuses = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => assetIds.Contains(item.AssetId))
            .ToListAsync(cancellationToken);
        var statusByAsset = statuses.ToDictionary(item => item.AssetId);
        foreach (Guid assetId in assetIds)
        {
            if (!statusByAsset.TryGetValue(assetId, out AssetCurrentStatusView? current) ||
                current.CurrentStatus is not (AssetCurrentStatus.InStock or AssetCurrentStatus.Issued or AssetCurrentStatus.InCustody))
            {
                return Result.Failure<Guid>(DisposalErrors.UnsupportedState(assetId));
            }
        }

        Guid[] materialIds = assets.Select(item => item.MaterialId).Distinct().ToArray();
        Dictionary<Guid, Guid> baseUnits = await (
                from material in context.Materials.AsNoTracking()
                join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
                where materialIds.Contains(material.Id)
                select new { material.Id, family.BaseUnitId })
            .ToDictionaryAsync(item => item.Id, item => item.BaseUnitId, cancellationToken);

        Result<WarehouseDocument> documentResult = await draftFactory.CreateAsync(
            command.WarehouseId, DocumentType.Adjustment, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result.Failure<Guid>(documentResult.Error);
        }

        WarehouseDocument document = documentResult.Value;
        Result<InventoryAdjustment> adjustment = InventoryAdjustment.Create(
            document.Id, null, AdjustmentKind.Disposal, command.Reason);
        if (adjustment.IsFailure)
        {
            return Result.Failure<Guid>(adjustment.Error);
        }

        context.WarehouseDocuments.Add(document);
        context.InventoryAdjustments.Add(adjustment.Value);
        foreach (Asset asset in assets)
        {
            var lineId = Guid.NewGuid();
            Result<DocumentLine> line = DocumentLine.Create(
                lineId, document.Id, asset.MaterialId, DocumentLineType.Asset,
                1m, baseUnits[asset.MaterialId], 1m, null, null, null);
            decimal difference = statusByAsset[asset.Id].CurrentStatus == AssetCurrentStatus.InStock ? -1m : 0m;
            Result<AdjustmentLine> detail = AdjustmentLine.Create(
                lineId, document.Id, difference, command.Reason, allowZero: true);
            Result<DocumentLineAssetSelection> selection = DocumentLineAssetSelection.Create(
                Guid.NewGuid(), document.Id, lineId, asset.Id);
            if (line.IsFailure || detail.IsFailure || selection.IsFailure)
            {
                Error error = line.IsFailure ? line.Error : detail.Error;
                if (line.IsSuccess && detail.IsSuccess)
                {
                    error = selection.Error;
                }
                return Result.Failure<Guid>(error);
            }

            context.DocumentLines.Add(line.Value);
            context.AdjustmentLines.Add(detail.Value);
            context.DocumentLineAssetSelections.Add(selection.Value);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(DisposalErrors.AlreadyPending(assetIds[0]));
        }

        return document.Id;
    }
}
