using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UnitsOfMeasure;
using SharedKernel;

namespace Application.UnitsOfMeasure.Create;

internal sealed class CreateUnitOfMeasureCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateUnitOfMeasureCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUnitOfMeasureCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.UnitsOfMeasure.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(UnitOfMeasureErrors.Forbidden);
        }

        var unit = UnitOfMeasure.Create(Guid.NewGuid(), command.Name, command.Symbol, command.UnitType);

        context.UnitsOfMeasure.Add(unit);

        await context.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}
