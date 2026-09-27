using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Materials;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials.Update;

internal sealed class UpdateMaterialCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IApplicationTransaction transaction,
    IMaterialOperationLock materialLock)
    : ICommandHandler<UpdateMaterialCommand>
{
    public Task<Result> Handle(UpdateMaterialCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            ct => UpdateInTransactionAsync(command, ct),
            cancellationToken);

    private async Task<Result> UpdateInTransactionAsync(
        UpdateMaterialCommand command,
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
            return Result.Failure(MaterialErrors.Forbidden);
        }

        Result classification = MaterialClassification.Validate(command.MaterialKind, command.TrackingType);

        if (classification.IsFailure)
        {
            return classification;
        }

        // The read, the operational-use guard and the write form one read-check-save sequence: taking
        // the material's advisory lock first makes them indivisible against a concurrent catalog edit
        // and against a document submit/post that reads the same material.
        await materialLock.AcquireAsync([command.MaterialId], cancellationToken);

        Material? material = await context.Materials
            .SingleOrDefaultAsync(m => m.Id == command.MaterialId, cancellationToken);

        if (material is null)
        {
            return Result.Failure(MaterialErrors.NotFound(command.MaterialId));
        }

        if (material.CatalogVersion != command.ExpectedCatalogVersion)
        {
            return Result.Failure(MaterialErrors.CatalogVersionMismatch(
                command.MaterialId,
                command.ExpectedCatalogVersion,
                material.CatalogVersion));
        }

        Result classificationLock = await MaterialOperationalUseGuard.EnsureClassificationMutableAsync(
            context,
            material,
            command.MaterialKind,
            command.TrackingType,
            cancellationToken);

        if (classificationLock.IsFailure)
        {
            return classificationLock;
        }

        material.UpdateDetails(
            command.NameAr,
            command.NameEn,
            command.MaterialKind,
            command.TrackingType,
            command.HasExpiry,
            command.Attributes);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // CatalogVersion is the material's EF concurrency token, so a row that moved between the
            // read above and this write - by another catalog edit or by a concurrent status change -
            // is reported as a conflict instead of silently overwriting the newer classification.
            int? currentVersion = await context.Materials
                .AsNoTracking()
                .Where(m => m.Id == command.MaterialId)
                .Select(m => (int?)m.CatalogVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(MaterialErrors.CatalogVersionMismatch(
                command.MaterialId,
                command.ExpectedCatalogVersion,
                currentVersion));
        }

        return Result.Success();
    }
}
