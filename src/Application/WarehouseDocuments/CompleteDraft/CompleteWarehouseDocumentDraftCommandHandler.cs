using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Idempotency;
using Application.Abstractions.Messaging;
using Application.Abstractions.WarehouseDocuments;
using Application.DocumentLines.Add;
using Application.DocumentLineAssetSelections.Add;
using Application.InventoryAdjustments.AddLine;
using Application.IssueTos.Upsert;
using Application.ReceivingInfos.Upsert;
using Application.ReturnInfos.Upsert;
using Application.TransferInfos.Upsert;
using Application.WarehouseDocuments.GetById;
using Domain.Common;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using System.Text.Json;

namespace Application.WarehouseDocuments.CompleteDraft;

internal sealed class CompleteWarehouseDocumentDraftCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IWarehouseDocumentDraftFactory draftFactory,
    IApplicationTransaction transaction,
    IIdempotencyService idempotencyService,
    ICommandHandler<AddDocumentLineCommand, Guid> addDocumentLineHandler,
    ICommandHandler<AddDocumentLineAssetSelectionCommand, Guid> addAssetSelectionHandler,
    ICommandHandler<AddAdjustmentLineCommand, Guid> addAdjustmentLineHandler,
    ICommandHandler<UpsertReceivingInfoCommand> upsertReceivingInfoHandler,
    ICommandHandler<UpsertIssueToCommand> upsertIssueToHandler,
    ICommandHandler<UpsertTransferInfoCommand> upsertTransferInfoHandler,
    ICommandHandler<UpsertReturnInfoCommand> upsertReturnInfoHandler,
    IQueryHandler<GetWarehouseDocumentByIdQuery, WarehouseDocumentDetailsResponse> getDocumentByIdHandler)
    : ICommandHandler<CompleteWarehouseDocumentDraftCommand, WarehouseDocumentDetailsResponse>
{
    public async Task<Result<WarehouseDocumentDetailsResponse>> Handle(
        CompleteWarehouseDocumentDraftCommand command,
        CancellationToken cancellationToken)
    {
        bool canCreate = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);
        if (!canCreate)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.Forbidden);
        }

        bool canEdit = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);
        bool canView = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);
        if (!canEdit || !canView)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.Forbidden);
        }

        Result detailsValidation = ValidateDetails(command);
        if (detailsValidation.IsFailure)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(detailsValidation.Error);
        }

        IdempotencyRequest? idempotencyRequest = command.IdempotencyKey.HasValue
            ? IdempotencyRequest.Create(command.IdempotencyKey.Value, "warehouse-document.complete-draft", Canonicalize(command))
            : null;

        return await transaction.ExecuteAsync(
            ct => CreateAggregateInTransactionAsync(command, idempotencyRequest, ct),
            cancellationToken);
    }

    private async Task<Result<WarehouseDocumentDetailsResponse>> CreateAggregateInTransactionAsync(
        CompleteWarehouseDocumentDraftCommand command,
        IdempotencyRequest? idempotencyRequest,
        CancellationToken cancellationToken)
    {
        if (idempotencyRequest is not null)
        {
            Result<IdempotencyReplay<WarehouseDocumentDetailsResponse>> begin = await idempotencyService.TryBeginAsync<WarehouseDocumentDetailsResponse>(
                idempotencyRequest, userContext.UserId, cancellationToken);
            if (begin.IsFailure)
            {
                return Result.Failure<WarehouseDocumentDetailsResponse>(begin.Error);
            }
            if (begin.Value.HasResponse)
            {
                return begin.Value.Response!;
            }
        }

        Result<Domain.WarehouseDocuments.WarehouseDocument> createResult = await draftFactory.CreateAsync(
            command.WarehouseId,
            command.DocumentType,
            cancellationToken);
        if (createResult.IsFailure)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(createResult.Error);
        }

        Domain.WarehouseDocuments.WarehouseDocument document = createResult.Value;
        context.WarehouseDocuments.Add(document);
        if (command.DocumentType == DocumentType.Adjustment)
        {
            Result<InventoryAdjustment> adjustment = InventoryAdjustment.Create(document.Id, null,
                AdjustmentKind.Quantity, command.AdjustmentInfo!.Reason);
            if (adjustment.IsFailure)
            {
                return Result.Failure<WarehouseDocumentDetailsResponse>(adjustment.Error);
            }
            context.InventoryAdjustments.Add(adjustment.Value);
        }
        await context.SaveChangesAsync(cancellationToken);

        var createdLines = new List<(Guid Id, CompleteWarehouseDocumentDraftLine Request)>();
        foreach (CompleteWarehouseDocumentDraftLine line in command.Lines)
        {
            int expectedRowVersion = await GetCurrentRowVersionAsync(document.Id, cancellationToken);
            if (command.DocumentType == DocumentType.Adjustment)
            {
                Result<Guid> adjustmentLine = await addAdjustmentLineHandler.Handle(new AddAdjustmentLineCommand(
                    document.Id, line.MaterialId, line.Difference!.Value, line.UnitId,
                    line.AdjustmentReason ?? command.AdjustmentInfo!.Reason, expectedRowVersion), cancellationToken);
                if (adjustmentLine.IsFailure)
                {
                    return Result.Failure<WarehouseDocumentDetailsResponse>(adjustmentLine.Error);
                }
                continue;
            }
            Result<Guid> lineResult = await addDocumentLineHandler.Handle(
                new AddDocumentLineCommand(
                    document.Id,
                    line.MaterialId,
                    line.Quantity!.Value,
                    line.UnitId,
                    line.UnitPrice,
                    line.BatchNumber,
                    line.ExpiryDate,
                    line.OpeningType,
                    expectedRowVersion),
                cancellationToken);
            if (lineResult.IsFailure)
            {
                return Result.Failure<WarehouseDocumentDetailsResponse>(lineResult.Error);
            }
            createdLines.Add((lineResult.Value, line));
        }

        int detailsExpectedRowVersion = await GetCurrentRowVersionAsync(document.Id, cancellationToken);
        Result detailResult = command.DocumentType == DocumentType.Adjustment
            ? Result.Success()
            : await UpsertDetailsAsync(command, document.Id, detailsExpectedRowVersion, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(detailResult.Error);
        }

        if (command.DocumentType is DocumentType.Issue or DocumentType.Return)
        {
            foreach ((Guid lineId, CompleteWarehouseDocumentDraftLine requestedLine) in createdLines)
            {
                var persistedLine = await context.DocumentLines.AsNoTracking()
                    .Where(item => item.Id == lineId)
                    .Select(item => new { item.LineType, item.BaseQuantity })
                    .SingleAsync(cancellationToken);
                if (persistedLine.LineType == DocumentLineType.Asset &&
                    (requestedLine.AssetIds is null || requestedLine.AssetIds.Count != persistedLine.BaseQuantity))
                {
                    return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.CompleteDraftAssetSelectionCount(lineId));
                }
                foreach (Guid assetId in requestedLine.AssetIds ?? [])
                {
                    int selectionVersion = await GetCurrentRowVersionAsync(document.Id, cancellationToken);
                    Result<Guid> selection = await addAssetSelectionHandler.Handle(new AddDocumentLineAssetSelectionCommand(
                        document.Id, lineId, assetId, selectionVersion), cancellationToken);
                    if (selection.IsFailure)
                    {
                        return Result.Failure<WarehouseDocumentDetailsResponse>(selection.Error);
                    }
                }
            }
        }

        Result<WarehouseDocumentDetailsResponse> response = await getDocumentByIdHandler.Handle(
            new GetWarehouseDocumentByIdQuery(document.Id), cancellationToken);
        if (response.IsSuccess && idempotencyRequest is not null)
        {
            idempotencyService.Complete(idempotencyRequest, userContext.UserId, response.Value);
            await context.SaveChangesAsync(cancellationToken);
        }
        return response;
    }

    private async Task<Result> UpsertDetailsAsync(
        CompleteWarehouseDocumentDraftCommand command,
        Guid documentId,
        int expectedRowVersion,
        CancellationToken cancellationToken) => command.DocumentType switch
    {
        DocumentType.Receiving => await upsertReceivingInfoHandler.Handle(
            new UpsertReceivingInfoCommand(
                documentId,
                command.ReceivingInfo!.SupplierPartyId,
                command.ReceivingInfo.SupplierInvoiceRef,
                ReceivingType.Supplier,
                expectedRowVersion),
            cancellationToken),
        DocumentType.Issue => await upsertIssueToHandler.Handle(
            new UpsertIssueToCommand(
                documentId,
                command.IssueTo!.RecipientType,
                command.IssueTo.RecipientId,
                command.IssueTo.IssueReason,
                expectedRowVersion),
            cancellationToken),
        DocumentType.Transfer => await upsertTransferInfoHandler.Handle(
            new UpsertTransferInfoCommand(
                documentId,
                command.TransferInfo!.DestinationWarehouseId,
                command.TransferInfo.TransferReason,
                expectedRowVersion),
            cancellationToken),
        DocumentType.Return => await upsertReturnInfoHandler.Handle(
            new UpsertReturnInfoCommand(
                documentId,
                command.ReturnInfo!.OriginalIssueDocumentId,
                command.ReturnInfo.ReturnReason,
                expectedRowVersion),
            cancellationToken),
        DocumentType.Opening => Result.Success(),
        _ => Result.Failure(WarehouseDocumentErrors.CompleteDraftUnsupportedType(command.DocumentType))
    };

    private async Task<int> GetCurrentRowVersionAsync(Guid documentId, CancellationToken cancellationToken) =>
        await context.WarehouseDocuments.AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => document.RowVersion)
            .SingleAsync(cancellationToken);

    private static Result ValidateDetails(CompleteWarehouseDocumentDraftCommand command)
    {
        bool detailsMatch = command.DocumentType switch
        {
            DocumentType.Receiving => command.ReceivingInfo is not null && command.IssueTo is null &&
                command.TransferInfo is null && command.ReturnInfo is null && command.AdjustmentInfo is null,
            DocumentType.Issue => command.ReceivingInfo is null && command.IssueTo is not null &&
                command.TransferInfo is null && command.ReturnInfo is null && command.AdjustmentInfo is null,
            DocumentType.Transfer => command.ReceivingInfo is null && command.IssueTo is null &&
                command.TransferInfo is not null && command.ReturnInfo is null && command.AdjustmentInfo is null,
            DocumentType.Return => command.ReceivingInfo is null && command.IssueTo is null &&
                command.TransferInfo is null && command.ReturnInfo is not null && command.AdjustmentInfo is null,
            DocumentType.Adjustment => command.AdjustmentInfo is { AdjustmentKind: AdjustmentKind.Quantity } &&
                command.ReceivingInfo is null && command.IssueTo is null && command.TransferInfo is null && command.ReturnInfo is null,
            DocumentType.Opening => command.ReceivingInfo is null && command.IssueTo is null &&
                command.TransferInfo is null && command.ReturnInfo is null && command.AdjustmentInfo is null,
            _ => false
        };

        return detailsMatch
            ? Result.Success()
            : Result.Failure(WarehouseDocumentErrors.CompleteDraftDetailsMismatch(command.DocumentType));
    }

    private static string Canonicalize(CompleteWarehouseDocumentDraftCommand command) => JsonSerializer.Serialize(new
    {
        command.WarehouseId, command.DocumentType, command.Lines, command.ReceivingInfo,
        command.IssueTo, command.TransferInfo, command.ReturnInfo, command.AdjustmentInfo
    });
}
