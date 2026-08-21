using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.WarehouseDocuments;
using Domain.Common;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.InventoryAdjustments.Create;

internal sealed class CreateInventoryAdjustmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IWarehouseDocumentDraftFactory draftFactory)
    : ICommandHandler<CreateInventoryAdjustmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateInventoryAdjustmentCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse, command.WarehouseId, cancellationToken);
        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.Forbidden);
        }

        Result<WarehouseDocument> document = await draftFactory.CreateAsync(
            command.WarehouseId, DocumentType.Adjustment, cancellationToken);
        if (document.IsFailure)
        {
            return Result.Failure<Guid>(document.Error);
        }

        Result<InventoryAdjustment> adjustment = InventoryAdjustment.Create(
            document.Value.Id, null, AdjustmentKind.Quantity, command.Reason);
        if (adjustment.IsFailure)
        {
            return Result.Failure<Guid>(adjustment.Error);
        }

        context.WarehouseDocuments.Add(document.Value);
        context.InventoryAdjustments.Add(adjustment.Value);
        await context.SaveChangesAsync(cancellationToken);
        return document.Value.Id;
    }
}
