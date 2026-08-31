using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials.Update;

internal sealed class UpdateMaterialCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateMaterialCommand>
{
    public async Task<Result> Handle(UpdateMaterialCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Materials.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(MaterialErrors.Forbidden);
        }

        Material? material = await context.Materials
            .SingleOrDefaultAsync(m => m.Id == command.MaterialId, cancellationToken);

        if (material is null)
        {
            return Result.Failure(MaterialErrors.NotFound(command.MaterialId));
        }

        if (command.MaterialKind == MaterialKind.Consumable && command.TrackingType != TrackingType.Quantity)
        {
            return Result.Failure(MaterialErrors.ConsumableMustBeQuantityTracked);
        }

        if (command.MaterialKind == MaterialKind.Asset && command.TrackingType != TrackingType.Serial)
        {
            return Result.Failure(MaterialErrors.AssetMustBeSerialTracked);
        }

        material.UpdateDetails(
            command.NameAr,
            command.NameEn,
            command.MaterialKind,
            command.TrackingType,
            command.HasExpiry,
            command.Attributes);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
