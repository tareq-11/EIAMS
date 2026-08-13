using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.ReceivingInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReceivingInfos.Upsert;

internal sealed class UpsertReceivingInfoCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<UpsertReceivingInfoCommand>
{
    public async Task<Result> Handle(UpsertReceivingInfoCommand command, CancellationToken cancellationToken)
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
                command.DocumentId,
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

        if (document.DocumentType != DocumentType.Receiving)
        {
            return Result.Failure(ReceivingInfoErrors.WrongDocumentType(document.Id));
        }

        ReceivingInfo? info = await context.ReceivingInfos
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        bool hasChanges;
        Result infoResult;

        if (info is null)
        {
            Result<ReceivingInfo> createResult = ReceivingInfo.Create(
                document.Id,
                command.SupplierRef,
                command.SupplierInvoiceRef,
                command.ReceivingType);

            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            context.ReceivingInfos.Add(createResult.Value);
            hasChanges = true;
            infoResult = Result.Success();
        }
        else
        {
            string normalizedSupplierRef = command.SupplierRef.Trim();
            string? normalizedInvoiceRef = string.IsNullOrWhiteSpace(command.SupplierInvoiceRef)
                ? null
                : command.SupplierInvoiceRef.Trim();

            hasChanges = info.SupplierRef != normalizedSupplierRef ||
                info.SupplierInvoiceRef != normalizedInvoiceRef ||
                info.ReceivingType != command.ReceivingType;

            infoResult = info.Update(
                command.SupplierRef,
                command.SupplierInvoiceRef,
                command.ReceivingType);
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
            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(item => item.Id == document.Id)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                document.Id,
                command.ExpectedRowVersion,
                currentRowVersion));
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "pk_receiving_info"))
        {
            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(item => item.Id == document.Id)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                document.Id,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        return Result.Success();
    }
}
