using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DurableCustodies;
using Domain.DurableCustodyAllocations;
using Domain.TrackedMaterialUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Custodies.Transfer;

internal sealed class TransferCustodyCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    ICounterpartResolver counterpartResolver,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<TransferCustodyCommand>
{
    public async Task<Result> Handle(TransferCustodyCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Custodies.Manage,
            ScopeType.Enterprise,
            null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(Error.Forbidden(
                "Custodies.Unauthorized",
                "You do not have permission to transfer custody."));
        }

        Result<CounterpartResolution> resolutionResult = await counterpartResolver.ValidateForWriteAsync(
            userContext.UserId,
            command.NewHolderType,
            command.NewHolderId,
            command.NewCustodyKind,
            cancellationToken);

        if (resolutionResult.IsFailure)
        {
            return Result.Failure(resolutionResult.Error);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        switch (command.SubjectType)
        {
            case CustodySubjectType.Asset:
                Custody? custody = await context.Custodies
                    .SingleOrDefaultAsync(c => c.Id == command.CustodyId, cancellationToken);

                if (custody is null)
                {
                    return Result.Failure(CustodyErrors.NotFound(command.CustodyId));
                }

                if (custody.RowVersion != command.ExpectedRowVersion)
                {
                    return Result.Failure(Error.Conflict(
                        "Custodies.RowVersionMismatch",
                        "Custody row version mismatch."));
                }

                if (custody.Status != CustodyStatus.Active)
                {
                    return Result.Failure(CustodyErrors.NotActive);
                }

                Result closeResult = custody.Close(custody.IssueDocumentId, nowUtc);
                if (closeResult.IsFailure)
                {
                    return closeResult;
                }

                Result<Custody> newCustodyResult = Custody.Open(
                    Guid.NewGuid(),
                    custody.AssetId,
                    command.NewHolderType,
                    command.NewHolderId,
                    command.NewCustodyKind,
                    custody.IssueDocumentId,
                    nowUtc);

                if (newCustodyResult.IsFailure)
                {
                    return Result.Failure(newCustodyResult.Error);
                }

                context.Custodies.Add(newCustodyResult.Value);

                Result<CustodyHistory> historyResult = CustodyHistory.Create(
                    Guid.NewGuid(),
                    custody.Id,
                    CustodyStatus.Active,
                    CustodyStatus.Closed,
                    userContext.UserId,
                    nowUtc,
                    command.Note ?? $"Transferred to {command.NewHolderType} {command.NewHolderId}");

                if (historyResult.IsFailure)
                {
                    return Result.Failure(historyResult.Error);
                }

                context.CustodyHistories.Add(historyResult.Value);
                break;

            case CustodySubjectType.TrackedUnit:
                TrackedMaterialUnit? unit = await context.TrackedMaterialUnits
                    .SingleOrDefaultAsync(u => u.Id == command.CustodyId, cancellationToken);

                if (unit is null)
                {
                    return Result.Failure(TrackedMaterialUnitErrors.NotFound(command.CustodyId));
                }

                if (unit.RowVersion != command.ExpectedRowVersion)
                {
                    return Result.Failure(Error.Conflict(
                        "Custodies.RowVersionMismatch",
                        "Tracked unit row version mismatch."));
                }

                PartyType oldUnitHolderType = unit.HolderType;
                Guid oldUnitHolderId = unit.HolderId;

                Result unitTransferResult = unit.Transfer(
                    command.NewHolderType,
                    command.NewHolderId,
                    command.NewCustodyKind,
                    nowUtc);

                if (unitTransferResult.IsFailure)
                {
                    return unitTransferResult;
                }

                context.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.TrackedUnit,
                    unit.Id,
                    "Transferred",
                    oldUnitHolderType,
                    oldUnitHolderId,
                    command.NewHolderType,
                    command.NewHolderId,
                    1m,
                    unit.IssueDocumentId,
                    nowUtc,
                    userContext.UserId,
                    command.Note));
                break;

            case CustodySubjectType.MaterialQuantity:
                DurableCustodyAllocation? allocation = await context.DurableCustodyAllocations
                    .SingleOrDefaultAsync(a => a.Id == command.CustodyId, cancellationToken);

                if (allocation is null)
                {
                    return Result.Failure(DurableCustodyAllocationErrors.NotFound(command.CustodyId));
                }

                if (allocation.RowVersion != command.ExpectedRowVersion)
                {
                    return Result.Failure(Error.Conflict(
                        "Custodies.RowVersionMismatch",
                        "Durable allocation row version mismatch."));
                }

                PartyType oldAllocHolderType = allocation.HolderType;
                Guid oldAllocHolderId = allocation.HolderId;

                Result allocTransferResult = allocation.Transfer(
                    command.NewHolderType,
                    command.NewHolderId,
                    command.NewCustodyKind,
                    nowUtc);

                if (allocTransferResult.IsFailure)
                {
                    return allocTransferResult;
                }

                context.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.MaterialQuantity,
                    allocation.Id,
                    "Transferred",
                    oldAllocHolderType,
                    oldAllocHolderId,
                    command.NewHolderType,
                    command.NewHolderId,
                    allocation.ActiveQuantity,
                    allocation.IssueDocumentId,
                    nowUtc,
                    userContext.UserId,
                    command.Note));
                break;

            default:
                return Result.Failure(Error.Problem("Custodies.InvalidSubjectType", "Invalid custody subject type."));
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict(
                "Custodies.ConcurrencyConflict",
                "A concurrency conflict occurred while transferring custody."));
        }

        return Result.Success();
    }
}
