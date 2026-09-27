using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitsOfMeasure.SetStatus;

internal sealed class SetUnitOfMeasureStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetUnitOfMeasureStatusCommand>
{
    public async Task<Result> Handle(SetUnitOfMeasureStatusCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.UnitsOfMeasure.Manage, ScopeType.Enterprise, null, cancellationToken);
        if (!authorized)
        {
            return Result.Failure(UnitOfMeasureErrors.Forbidden);
        }

        UnitOfMeasure? unit = await context.UnitsOfMeasure
            .SingleOrDefaultAsync(u => u.Id == command.UnitOfMeasureId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure(UnitOfMeasureErrors.NotFound(command.UnitOfMeasureId));
        }

        if (command.Status == Status.Inactive && await IsInUseAsync(context, unit.Id, cancellationToken))
        {
            return Result.Failure(UnitOfMeasureErrors.InUse(unit.Id));
        }

        unit.SetStatus(command.Status);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static async Task<bool> IsInUseAsync(IApplicationDbContext context, Guid unitId, CancellationToken ct) =>
        await context.Materials.AnyAsync(m => m.BaseUnitId == unitId, ct) ||
        await context.MaterialUnitConversions.AnyAsync(c => c.FromUnitId == unitId || c.ToBaseUnitId == unitId, ct);
}
