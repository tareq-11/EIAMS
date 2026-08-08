using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Sites;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Warehouses.Create;

internal sealed class CreateWarehouseCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateWarehouseCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.Manage,
            ScopeType.Site,
            command.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseErrors.Forbidden);
        }

        Site? site = await context.Sites.SingleOrDefaultAsync(s => s.Id == command.SiteId, cancellationToken);

        if (site is null)
        {
            return Result.Failure<Guid>(WarehouseErrors.SiteNotFound(command.SiteId));
        }

        if (site.Status != Status.Active)
        {
            return Result.Failure<Guid>(WarehouseErrors.SiteInactive(command.SiteId));
        }

        if (await context.Warehouses.AnyAsync(w => w.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(WarehouseErrors.CodeNotUnique(command.Code));
        }

        var warehouse = Warehouse.Create(
            Guid.NewGuid(),
            command.SiteId,
            command.Name,
            command.Code,
            command.WarehouseType,
            command.CanHoldStock);

        context.Warehouses.Add(warehouse);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(WarehouseErrors.CodeNotUnique(command.Code));
        }

        return warehouse.Id;
    }
}
