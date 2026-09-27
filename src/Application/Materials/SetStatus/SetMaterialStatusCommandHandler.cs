using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials.SetStatus;

internal sealed class SetMaterialStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetMaterialStatusCommand>
{
    public async Task<Result> Handle(SetMaterialStatusCommand command, CancellationToken cancellationToken)
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

        Result statusResult = material.SetStatus(command.Status);

        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // CatalogVersion is the material's EF concurrency token, so a status change that raced a
            // catalog edit is reported as a conflict instead of a server error.
            int? currentVersion = await context.Materials
                .AsNoTracking()
                .Where(m => m.Id == command.MaterialId)
                .Select(m => (int?)m.CatalogVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(MaterialErrors.CatalogVersionMismatch(
                command.MaterialId,
                material.CatalogVersion,
                currentVersion));
        }

        return Result.Success();
    }
}
