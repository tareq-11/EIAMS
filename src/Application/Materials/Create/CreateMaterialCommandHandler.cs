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

        Result classificationChain = await EnsureActiveClassificationChainAsync(
            context,
            command.FamilyId,
            cancellationToken);

        if (classificationChain.IsFailure)
        {
            return Result.Failure<Guid>(classificationChain.Error);
        }

        var baseUnit = await context.UnitsOfMeasure
            .Where(u => u.Id == command.BaseUnitId)
            .Select(u => new { u.Id, u.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (baseUnit is null)
        {
            return Result.Failure<Guid>(MaterialErrors.UnitNotFound(command.BaseUnitId));
        }
        if (baseUnit.Status != Status.Active)
        {
            return Result.Failure<Guid>(MaterialErrors.UnitNotActive(command.BaseUnitId));
        }

        Result classification = MaterialClassification.Validate(command.MaterialKind, command.TrackingType);

        if (classification.IsFailure)
        {
            return Result.Failure<Guid>(classification.Error);
        }

        if (await context.Materials.AnyAsync(m => m.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialErrors.CodeNotUnique);
        }

        var material = Material.Create(
            Guid.NewGuid(),
            command.FamilyId,
            command.BaseUnitId,
            command.NameAr,
            command.NameEn,
            command.Code,
            command.MaterialKind,
            command.TrackingType,
            command.HasExpiry,
            command.Attributes);

        context.Materials.Add(material);

        await context.SaveChangesAsync(cancellationToken);

        return material.Id;
    }

    /// <summary>
    /// A material is only classifiable under a family whose whole chain (family, category, domain) is
    /// present and active; the domain is still derived through the hierarchy and never accepted as
    /// an input.
    /// </summary>
    private static async Task<Result> EnsureActiveClassificationChainAsync(
        IApplicationDbContext context,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var chain = await (
            from family in context.MaterialFamilies.AsNoTracking()
            where family.Id == familyId
            join category in context.MaterialCategories.AsNoTracking()
                on family.CategoryId equals category.Id into categoryGroup
            from category in categoryGroup.DefaultIfEmpty()
            join domain in context.MaterialDomains.AsNoTracking()
                on category.MaterialDomainId equals domain.Id into domainGroup
            from domain in domainGroup.DefaultIfEmpty()
            select new
            {
                FamilyActive = family.Status == Status.Active,
                CategoryActive = category != null && category.Status == Status.Active,
                DomainActive = domain != null && domain.Status == Status.Active
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (chain is null || !chain.FamilyActive)
        {
            return Result.Failure(MaterialErrors.FamilyNotActive);
        }

        if (!chain.CategoryActive)
        {
            return Result.Failure(MaterialErrors.CategoryNotActive);
        }

        return chain.DomainActive ? Result.Success() : Result.Failure(MaterialErrors.DomainNotActive);
    }
}
