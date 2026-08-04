using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitsOfMeasure.Update;

internal sealed class UpdateUnitOfMeasureCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateUnitOfMeasureCommand>
{
    public async Task<Result> Handle(UpdateUnitOfMeasureCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.UnitsOfMeasure.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

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

        unit.UpdateDetails(command.Name, command.Symbol, command.UnitType);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
