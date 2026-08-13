using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.Common;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.Create;

internal sealed class CreateWarehouseDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IReferenceNumberGenerator referenceNumberGenerator)
    : ICommandHandler<CreateWarehouseDocumentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWarehouseDocumentCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.Forbidden);
        }

        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        if (warehouse.Status != Status.Active)
        {
            return Result.Failure<Guid>(WarehouseErrors.Inactive(command.WarehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure<Guid>(WarehouseErrors.CannotHoldStock(command.WarehouseId));
        }

        Result<string> referenceResult = await referenceNumberGenerator.AllocateAsync(
            warehouse.SiteId,
            command.DocumentType,
            cancellationToken);

        if (referenceResult.IsFailure)
        {
            return Result.Failure<Guid>(referenceResult.Error);
        }

        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            command.WarehouseId,
            command.DocumentType,
            referenceResult.Value);

        context.WarehouseDocuments.Add(document);

        await context.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
