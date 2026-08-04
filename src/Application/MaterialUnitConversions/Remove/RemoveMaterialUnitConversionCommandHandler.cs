using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialUnitConversions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions.Remove;

internal sealed class RemoveMaterialUnitConversionCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemoveMaterialUnitConversionCommand>
{
    public async Task<Result> Handle(RemoveMaterialUnitConversionCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Materials.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(MaterialUnitConversionErrors.Forbidden);
        }

        MaterialUnitConversion? conversion = await context.MaterialUnitConversions
            .SingleOrDefaultAsync(c => c.Id == command.MaterialUnitConversionId, cancellationToken);

        if (conversion is null)
        {
            return Result.Failure(MaterialUnitConversionErrors.NotFound(command.MaterialUnitConversionId));
        }

        conversion.MarkAsRemoved();
        context.MaterialUnitConversions.Remove(conversion);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
