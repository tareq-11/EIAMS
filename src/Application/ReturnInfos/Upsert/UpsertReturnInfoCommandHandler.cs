using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReturnInfos.Upsert;

internal sealed class UpsertReturnInfoCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<UpsertReturnInfoCommand>
{
    public async Task<Result> Handle(UpsertReturnInfoCommand command, CancellationToken cancellationToken)
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

        if (document.DocumentType != DocumentType.Return)
        {
            return Result.Failure(ReturnInfoErrors.WrongDocumentType(document.Id));
        }

        WarehouseDocument? originalIssue = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.OriginalIssueDocumentId, cancellationToken);

        if (originalIssue is null ||
            originalIssue.DocumentType != DocumentType.Issue ||
            originalIssue.DocumentStatus != DocumentStatus.Posted)
        {
            return Result.Failure(ReturnInfoErrors.OriginalIssueInvalid(command.OriginalIssueDocumentId));
        }

        if (originalIssue.WarehouseId != document.WarehouseId)
        {
            return Result.Failure(ReturnInfoErrors.WrongWarehouse(
                document.Id,
                document.WarehouseId));
        }

        ReturnInfo? returnInfo = await context.ReturnInfos
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        bool hasChanges;
        Result infoResult;

        if (returnInfo is null)
        {
            Result<ReturnInfo> createResult = ReturnInfo.Create(
                document.Id,
                command.OriginalIssueDocumentId,
                command.ReturnReason);

            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            context.ReturnInfos.Add(createResult.Value);
            hasChanges = true;
            infoResult = Result.Success();
        }
        else
        {
            string normalizedReason = command.ReturnReason.Trim();
            hasChanges = returnInfo.OriginalIssueDocumentId != command.OriginalIssueDocumentId ||
                returnInfo.ReturnReason != normalizedReason;
            infoResult = returnInfo.Update(command.OriginalIssueDocumentId, command.ReturnReason);
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
            return await GetRowVersionMismatchAsync(command, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "pk_return_info"))
        {
            return await GetRowVersionMismatchAsync(command, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result> GetRowVersionMismatchAsync(
        UpsertReturnInfoCommand command,
        CancellationToken cancellationToken)
    {
        int? currentRowVersion = await context.WarehouseDocuments
            .AsNoTracking()
            .Where(item => item.Id == command.DocumentId)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);

        return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
            command.DocumentId,
            command.ExpectedRowVersion,
            currentRowVersion));
    }
}
