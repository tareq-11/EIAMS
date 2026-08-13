using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.TransferInfos.Upsert;

internal sealed class UpsertTransferInfoCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<UpsertTransferInfoCommand>
{
    public async Task<Result> Handle(UpsertTransferInfoCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken);

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

        if (document.DocumentType != DocumentType.Transfer)
        {
            return Result.Failure(TransferInfoErrors.WrongDocumentType(document.Id));
        }

        if (document.WarehouseId == command.DestinationWarehouseId)
        {
            return Result.Failure(TransferInfoErrors.DestinationSameAsSource(
                document.Id,
                document.WarehouseId));
        }

        Warehouse? destination = await context.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                warehouse => warehouse.Id == command.DestinationWarehouseId,
                cancellationToken);

        if (destination is null)
        {
            return Result.Failure(WarehouseErrors.NotFound(command.DestinationWarehouseId));
        }

        if (destination.Status != Status.Active)
        {
            return Result.Failure(WarehouseErrors.Inactive(destination.Id));
        }

        if (!destination.CanHoldStock)
        {
            return Result.Failure(WarehouseErrors.CannotHoldStock(destination.Id));
        }

        TransferInfo? info = await context.TransferInfos
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        bool hasChanges;
        Result infoResult;

        if (info is null)
        {
            Result<TransferInfo> createResult = TransferInfo.Create(
                document.Id,
                command.DestinationWarehouseId,
                command.TransferReason);

            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            context.TransferInfos.Add(createResult.Value);
            hasChanges = true;
            infoResult = Result.Success();
        }
        else
        {
            hasChanges = info.DestinationWarehouseId != command.DestinationWarehouseId ||
                info.TransferReason != command.TransferReason.Trim();
            infoResult = info.Update(command.DestinationWarehouseId, command.TransferReason);
        }

        if (infoResult.IsFailure)
        {
            return infoResult;
        }

        if (!hasChanges)
        {
            return Result.Success();
        }

        Result mutationResult = document.RegisterDetailMutation();

        if (mutationResult.IsFailure)
        {
            return mutationResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(await CreateConcurrencyErrorAsync(document.Id, command, cancellationToken));
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "pk_transfer_info"))
        {
            return Result.Failure(await CreateConcurrencyErrorAsync(document.Id, command, cancellationToken));
        }

        return Result.Success();
    }

    private async Task<Error> CreateConcurrencyErrorAsync(
        Guid documentId,
        UpsertTransferInfoCommand command,
        CancellationToken cancellationToken)
    {
        int? currentRowVersion = await context.WarehouseDocuments
            .AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);

        return WarehouseDocumentErrors.RowVersionMismatch(
            documentId,
            command.ExpectedRowVersion,
            currentRowVersion);
    }
}
