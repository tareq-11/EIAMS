using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials.Create;

internal sealed class CreateMaterialCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateMaterialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMaterialCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Materials.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(MaterialErrors.Forbidden);
        }

        if (!await context.MaterialFamilies.AnyAsync(f => f.Id == command.FamilyId, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialErrors.FamilyNotFound(command.FamilyId));
        }

        if (await context.Materials.AnyAsync(m => m.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialErrors.CodeNotUnique);
        }

        var material = Material.Create(
            Guid.NewGuid(),
            command.FamilyId,
            command.NameAr,
            command.NameEn,
            command.Code,
            command.MaterialKind,
            command.TrackingType,
            command.HasExpiry,
            command.RequiresAssetNumber,
            command.Attributes);

        context.Materials.Add(material);

        await context.SaveChangesAsync(cancellationToken);

        return material.Id;
    }
}
