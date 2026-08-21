using Application.Abstractions.Assets;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Custodies.Assign;

internal sealed class AssignAssetCustodyCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IAssetKeyLock assetKeyLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IActivePartyLookup activePartyLookup,
    IDateTimeProvider dateTimeProvider,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<AssignAssetCustodyCommand, Guid>
{
    public Task<Result<Guid>> Handle(
        AssignAssetCustodyCommand command,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(ct => AssignInTransactionAsync(command, ct), cancellationToken);

    private async Task<Result<Guid>> AssignInTransactionAsync(
        AssignAssetCustodyCommand command,
        CancellationToken cancellationToken)
    {
        await assetKeyLock.AcquireAsync([command.AssetId], cancellationToken);

        Custody? current = await context.Custodies.SingleOrDefaultAsync(
            item => item.AssetId == command.AssetId && item.Status == CustodyStatus.Active,
            cancellationToken);

        if (current is null)
        {
            return Result.Failure<Guid>(CustodyErrors.NoActiveCustody(command.AssetId));
        }

        Guid? warehouseId = await context.WarehouseDocuments.AsNoTracking()
            .Where(item => item.Id == current.IssueDocumentId)
            .Select(item => (Guid?)item.WarehouseId)
            .SingleOrDefaultAsync(cancellationToken);

        if (warehouseId is null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(current.IssueDocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            warehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(current.IssueDocumentId));
        }

        if (current.RowVersion != command.ExpectedCustodyRowVersion)
        {
            return Result.Failure<Guid>(CustodyErrors.RowVersionMismatch(
                current.Id,
                command.ExpectedCustodyRowVersion,
                current.RowVersion));
        }

        if (current.CustodyKind != CustodyKind.Operational)
        {
            return Result.Failure<Guid>(CustodyErrors.NotOperational(current.Id));
        }

        ActivePartyLookupStatus employeeStatus = await activePartyLookup.GetStatusAsync(
            PartyType.Employee,
            command.EmployeeId,
            cancellationToken);

        Error? employeeError = employeeStatus switch
        {
            ActivePartyLookupStatus.Active => null,
            ActivePartyLookupStatus.NotFound => CustodyErrors.HolderNotFound(
                PartyType.Employee,
                command.EmployeeId),
            ActivePartyLookupStatus.Inactive => CustodyErrors.HolderInactive(
                PartyType.Employee,
                command.EmployeeId),
            _ => CustodyErrors.ExternalHolderNotSupported
        };

        if (employeeError is not null)
        {
            return Result.Failure<Guid>(employeeError);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;
        Result closeResult = current.Close(null, nowUtc);

        if (closeResult.IsFailure)
        {
            return Result.Failure<Guid>(closeResult.Error);
        }

        var newCustodyId = Guid.NewGuid();
        Result<Custody> openResult = Custody.Open(
            newCustodyId,
            command.AssetId,
            PartyType.Employee,
            command.EmployeeId,
            CustodyKind.Personal,
            current.IssueDocumentId,
            nowUtc);

        if (openResult.IsFailure)
        {
            return Result.Failure<Guid>(openResult.Error);
        }

        Result<CustodyHistory> historyResult = CustodyHistory.Create(
            Guid.NewGuid(),
            current.Id,
            CustodyStatus.Active,
            CustodyStatus.Closed,
            userContext.UserId,
            nowUtc,
            command.Note);

        if (historyResult.IsFailure)
        {
            return Result.Failure<Guid>(historyResult.Error);
        }

        context.Custodies.Add(openResult.Value);
        context.CustodyHistories.Add(historyResult.Value);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<Guid>(CustodyErrors.RowVersionMismatch(
                current.Id,
                command.ExpectedCustodyRowVersion,
                null));
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(CustodyErrors.ActiveCustodyExists(command.AssetId));
        }

        return newCustodyId;
    }
}
