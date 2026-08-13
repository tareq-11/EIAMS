using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLines;

internal sealed record DocumentLineCatalogContext(
    Material Material,
    MaterialFamily Family,
    MaterialUnitConversion? Conversion);

internal static class DocumentLineCatalogResolver
{
    public static async Task<Result<DocumentLineCatalogContext>> ResolveAsync(
        IApplicationDbContext context,
        Guid materialId,
        Guid? unitId,
        CancellationToken cancellationToken)
    {
        Material? material = await context.Materials
            .SingleOrDefaultAsync(m => m.Id == materialId, cancellationToken);

        if (material is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialErrors.NotFound(materialId));
        }

        if (material.Status != MaterialStatus.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialNotActive(materialId));
        }

        MaterialFamily? family = await context.MaterialFamilies
            .SingleOrDefaultAsync(f => f.Id == material.FamilyId, cancellationToken);

        if (family is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialFamilyErrors.NotFound(material.FamilyId));
        }

        if (family.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialFamilyNotActive(family.Id));
        }

        MaterialCategory? category = await context.MaterialCategories
            .SingleOrDefaultAsync(c => c.Id == family.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialCategoryErrors.NotFound(family.CategoryId));
        }

        if (category.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialCategoryNotActive(category.Id));
        }

        MaterialDomain? domain = await context.MaterialDomains
            .SingleOrDefaultAsync(d => d.Id == category.MaterialDomainId, cancellationToken);

        if (domain is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialDomainErrors.NotFound(category.MaterialDomainId));
        }

        if (domain.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialDomainNotActive(domain.Id));
        }

        if (!await context.UnitsOfMeasure.AnyAsync(u => u.Id == family.BaseUnitId, cancellationToken))
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.UnitNotFound(family.BaseUnitId));
        }

        MaterialUnitConversion? conversion = null;

        if (unitId is not null && unitId != family.BaseUnitId)
        {
            if (!await context.UnitsOfMeasure.AnyAsync(u => u.Id == unitId, cancellationToken))
            {
                return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.UnitNotFound(unitId.Value));
            }

            conversion = await context.MaterialUnitConversions.SingleOrDefaultAsync(
                c => c.MaterialId == materialId &&
                     c.FromUnitId == unitId &&
                     c.ToBaseUnitId == family.BaseUnitId,
                cancellationToken);

            if (conversion is null)
            {
                return Result.Failure<DocumentLineCatalogContext>(
                    DocumentLineErrors.UnitConversionNotFound(materialId, unitId.Value));
            }
        }

        return new DocumentLineCatalogContext(material, family, conversion);
    }
}
