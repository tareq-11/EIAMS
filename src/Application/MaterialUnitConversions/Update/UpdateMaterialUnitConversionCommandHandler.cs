using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialUnitConversions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions.Update;

internal sealed class UpdateMaterialUnitConversionCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateMaterialUnitConversionCommand>
{
    public async Task<Result> Handle(
        UpdateMaterialUnitConversionCommand command,
        CancellationToken cancellationToken)
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

        if (command.Factor <= 0)
        {
            return Result.Failure(MaterialUnitConversionErrors.FactorMustBePositive);
        }

        MaterialUnitConversion? conversion = await context.MaterialUnitConversions
            .SingleOrDefaultAsync(
                c => c.MaterialId == command.MaterialId && c.Id == command.ConversionId,
                cancellationToken);

        if (conversion is null)
        {
            return Result.Failure(MaterialUnitConversionErrors.NotFound(command.ConversionId));
        }

        conversion.UpdateFactor(command.Factor);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
