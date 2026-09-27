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
    MaterialUnitConversion? Conversion)
{
    /// <summary>
    /// The one base unit of the material (<c>Material.BaseUnitId</c>, decision 038). The family value
    /// is never used for quantity semantics.
    /// </summary>
    public Guid BaseUnitId => Material.BaseUnitId;

    /// <summary>
    /// Freezes the catalog state this line is written against, so a later catalog edit cannot
    /// reinterpret the line (3B provenance). The capture is validated rather than defaulted: a
    /// material with no base unit or a conversion with no factor fails the line write instead of
    /// storing a snapshot that cannot be interpreted later.
    /// </summary>
    public Result<DocumentLineProvenance> CaptureProvenance() =>
        DocumentLineProvenance.Create(
            Material.CatalogVersion,
            Material.MaterialKind,
            Material.TrackingType,
            Material.BaseUnitId,
            Conversion?.Id,
            Conversion?.FromUnitId,
            Conversion?.ToBaseUnitId,
            Conversion?.Factor);
}

internal static class DocumentLineCatalogResolver
{
    public static async Task<Result<DocumentLineCatalogContext>> ResolveAsync(
        IApplicationDbContext context,
        Guid materialId,
        Guid? unitId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from m in context.Materials.AsNoTracking()
            where m.Id == materialId
            join f in context.MaterialFamilies.AsNoTracking() on m.FamilyId equals f.Id into fGroup
            from f in fGroup.DefaultIfEmpty()
            join c in context.MaterialCategories.AsNoTracking() on f.CategoryId equals c.Id into cGroup
            from c in cGroup.DefaultIfEmpty()
            join d in context.MaterialDomains.AsNoTracking() on c.MaterialDomainId equals d.Id into dGroup
            from d in dGroup.DefaultIfEmpty()
            join bu in context.UnitsOfMeasure.AsNoTracking() on m.BaseUnitId equals bu.Id into buGroup
            from bu in buGroup.DefaultIfEmpty()
            select new
            {
                Material = m,
                Family = f,
                Category = c,
                Domain = d,
                BaseUnitExists = bu != null
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (row is null || row.Material is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialErrors.NotFound(materialId));
        }

        if (row.Material.Status != MaterialStatus.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialNotActive(materialId));
        }

        if (row.Family is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialFamilyErrors.NotFound(row.Material.FamilyId));
        }

        if (row.Family.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialFamilyNotActive(row.Family.Id));
        }

        if (row.Category is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialCategoryErrors.NotFound(row.Family.CategoryId));
        }

        if (row.Category.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialCategoryNotActive(row.Category.Id));
        }

        if (row.Domain is null)
        {
            return Result.Failure<DocumentLineCatalogContext>(MaterialDomainErrors.NotFound(row.Category.MaterialDomainId));
        }

        if (row.Domain.Status != Status.Active)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.MaterialDomainNotActive(row.Domain.Id));
        }

        if (!row.BaseUnitExists)
        {
            return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.UnitNotFound(row.Material.BaseUnitId));
        }

        MaterialUnitConversion? conversion = null;

        if (unitId is not null && unitId != row.Material.BaseUnitId)
        {
            if (!await context.UnitsOfMeasure.AsNoTracking().AnyAsync(u => u.Id == unitId, cancellationToken))
            {
                return Result.Failure<DocumentLineCatalogContext>(DocumentLineErrors.UnitNotFound(unitId.Value));
            }

            conversion = await context.MaterialUnitConversions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    c => c.MaterialId == materialId &&
                         c.FromUnitId == unitId &&
                         c.ToBaseUnitId == row.Material.BaseUnitId,
                    cancellationToken);

            if (conversion is null)
            {
                return Result.Failure<DocumentLineCatalogContext>(
                    DocumentLineErrors.UnitConversionNotFound(materialId, unitId.Value));
            }
        }

        return new DocumentLineCatalogContext(row.Material, row.Family, conversion);
    }
}
