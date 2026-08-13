using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.IssueTos.Upsert;

internal sealed class UpsertIssueToCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IActivePartyLookup activePartyLookup,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<UpsertIssueToCommand>
{
    public async Task<Result> Handle(UpsertIssueToCommand command, CancellationToken cancellationToken)
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

        if (document.DocumentType != DocumentType.Issue)
        {
            return Result.Failure(IssueToErrors.WrongDocumentType(document.Id));
        }

        if (!Enum.IsDefined(command.RecipientType))
        {
            return Result.Failure(IssueToErrors.RecipientTypeInvalid);
        }

        if (command.RecipientId == Guid.Empty)
        {
            return Result.Failure(IssueToErrors.RecipientRequired);
        }

        Result recipientResult = await ValidateRecipientAsync(
            command.RecipientType,
            command.RecipientId,
            cancellationToken);

        if (recipientResult.IsFailure)
        {
            return recipientResult;
        }

        IssueTo? issueTo = await context.IssueTos
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        bool hasChanges;
        Result issueToResult;

        if (issueTo is null)
        {
            Result<IssueTo> createResult = IssueTo.Create(
                document.Id,
                command.RecipientType,
                command.RecipientId,
                command.IssueReason);

            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            context.IssueTos.Add(createResult.Value);
            hasChanges = true;
            issueToResult = Result.Success();
        }
        else
        {
            string normalizedReason = command.IssueReason.Trim();
            hasChanges = issueTo.RecipientType != command.RecipientType ||
                issueTo.RecipientId != command.RecipientId ||
                issueTo.IssueReason != normalizedReason;

            issueToResult = issueTo.Update(
                command.RecipientType,
                command.RecipientId,
                command.IssueReason);
        }

        if (issueToResult.IsFailure)
        {
            return issueToResult;
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
            return await GetRowVersionMismatchAsync(command.DocumentId, command.ExpectedRowVersion, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "pk_issue_to"))
        {
            return await GetRowVersionMismatchAsync(command.DocumentId, command.ExpectedRowVersion, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result> ValidateRecipientAsync(
        PartyType recipientType,
        Guid recipientId,
        CancellationToken cancellationToken)
    {
        ActivePartyLookupStatus status = await activePartyLookup.GetStatusAsync(
            recipientType,
            recipientId,
            cancellationToken);

        return status switch
        {
            ActivePartyLookupStatus.Active => Result.Success(),
            ActivePartyLookupStatus.NotFound => Result.Failure(
                IssueToErrors.RecipientNotFound(recipientType, recipientId)),
            ActivePartyLookupStatus.Inactive => Result.Failure(
                IssueToErrors.RecipientInactive(recipientType, recipientId)),
            _ => Result.Failure(IssueToErrors.ExternalRecipientNotSupported)
        };
    }

    private async Task<Result> GetRowVersionMismatchAsync(
        Guid documentId,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        int? currentRowVersion = await context.WarehouseDocuments
            .AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);

        return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
            documentId,
            expectedRowVersion,
            currentRowVersion));
    }
}
