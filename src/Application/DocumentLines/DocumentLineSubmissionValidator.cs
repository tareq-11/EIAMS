using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLines;

internal static class DocumentLineSubmissionValidator
{
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        CancellationToken cancellationToken)
    {
        List<DocumentLine> lines = await context.DocumentLines
            .AsNoTracking()
            .Where(line => line.DocumentId == document.Id)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return Result.Failure(WarehouseDocumentErrors.LinesRequired(document.Id));
        }

        return document.ReversalOfDocumentId is null
            ? await ValidateOperationalLinesAsync(context, document.Id, lines, cancellationToken)
            : await ValidateReversalLinesAsync(
                context,
                document.Id,
                document.ReversalOfDocumentId.Value,
                lines,
                cancellationToken);
    }

    private static async Task<Result> ValidateOperationalLinesAsync(
        IApplicationDbContext context,
        Guid documentId,
        List<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        Guid[] materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();

        List<Material> materials = await context.Materials
            .AsNoTracking()
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync(cancellationToken);
        var materialById = materials.ToDictionary(material => material.Id);

        Guid[] familyIds = materials.Select(material => material.FamilyId).Distinct().ToArray();
        List<MaterialFamily> families = await context.MaterialFamilies
            .AsNoTracking()
            .Where(family => familyIds.Contains(family.Id))
            .ToListAsync(cancellationToken);
        var familyById = families.ToDictionary(family => family.Id);

        Guid[] categoryIds = families.Select(family => family.CategoryId).Distinct().ToArray();
        List<MaterialCategory> categories = await context.MaterialCategories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
        var categoryById = categories.ToDictionary(category => category.Id);

        Guid[] materialDomainIds = categories
            .Select(category => category.MaterialDomainId)
            .Distinct()
            .ToArray();
        List<MaterialDomain> materialDomains = await context.MaterialDomains
            .AsNoTracking()
            .Where(domain => materialDomainIds.Contains(domain.Id))
            .ToListAsync(cancellationToken);
        var materialDomainById = materialDomains.ToDictionary(domain => domain.Id);

        Guid[] unitIds = families
            .Select(family => family.BaseUnitId)
            .Concat(lines.Where(line => line.UnitId is not null).Select(line => line.UnitId!.Value))
            .Distinct()
            .ToArray();
        var existingUnitIds = (await context.UnitsOfMeasure
                .AsNoTracking()
                .Where(unit => unitIds.Contains(unit.Id))
                .Select(unit => unit.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        Guid[] requestedConversionUnitIds = lines
            .Where(line => line.UnitId is not null)
            .Select(line => line.UnitId!.Value)
            .Distinct()
            .ToArray();
        List<MaterialUnitConversion> conversions = requestedConversionUnitIds.Length == 0
            ? []
            : await context.MaterialUnitConversions
                .AsNoTracking()
                .Where(conversion =>
                    materialIds.Contains(conversion.MaterialId) &&
                    requestedConversionUnitIds.Contains(conversion.FromUnitId))
                .ToListAsync(cancellationToken);
        var conversionByMaterialAndUnit = conversions.ToDictionary(
            conversion => (conversion.MaterialId, conversion.FromUnitId));

        foreach (DocumentLine line in lines)
        {
            if (!materialById.TryGetValue(line.MaterialId, out Material? material))
            {
                return Result.Failure(MaterialErrors.NotFound(line.MaterialId));
            }

            if (material.Status != MaterialStatus.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialNotActive(material.Id));
            }

            if (!familyById.TryGetValue(material.FamilyId, out MaterialFamily? family))
            {
                return Result.Failure(MaterialFamilyErrors.NotFound(material.FamilyId));
            }

            if (family.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialFamilyNotActive(family.Id));
            }

            if (!categoryById.TryGetValue(family.CategoryId, out MaterialCategory? category))
            {
                return Result.Failure(MaterialCategoryErrors.NotFound(family.CategoryId));
            }

            if (category.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialCategoryNotActive(category.Id));
            }

            if (!materialDomainById.TryGetValue(category.MaterialDomainId, out MaterialDomain? materialDomain))
            {
                return Result.Failure(MaterialDomainErrors.NotFound(category.MaterialDomainId));
            }

            if (materialDomain.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialDomainNotActive(materialDomain.Id));
            }

            if (!existingUnitIds.Contains(family.BaseUnitId))
            {
                return Result.Failure(DocumentLineErrors.UnitNotFound(family.BaseUnitId));
            }

            MaterialUnitConversion? conversion = null;

            if (line.UnitId is not null && line.UnitId != family.BaseUnitId)
            {
                if (!existingUnitIds.Contains(line.UnitId.Value))
                {
                    return Result.Failure(DocumentLineErrors.UnitNotFound(line.UnitId.Value));
                }

                if (!conversionByMaterialAndUnit.TryGetValue(
                        (line.MaterialId, line.UnitId.Value),
                        out conversion) ||
                    conversion.ToBaseUnitId != family.BaseUnitId)
                {
                    return Result.Failure(DocumentLineErrors.UnitConversionNotFound(
                        line.MaterialId,
                        line.UnitId.Value));
                }
            }

            Result<decimal> baseQuantityResult = BaseQuantityCalculator.Calculate(
                line.MaterialId,
                line.Quantity,
                line.UnitId,
                family.BaseUnitId,
                conversion);

            if (baseQuantityResult.IsFailure)
            {
                return Result.Failure(baseQuantityResult.Error);
            }

            if (line.BaseQuantity != baseQuantityResult.Value)
            {
                return Result.Failure(DocumentLineErrors.BaseQuantityMismatch(
                    documentId,
                    line.Id,
                    line.BaseQuantity,
                    baseQuantityResult.Value));
            }

            DocumentLineType expectedLineType = material.MaterialKind == MaterialKind.Asset
                ? DocumentLineType.Asset
                : DocumentLineType.Normal;

            if (line.LineType != expectedLineType)
            {
                return Result.Failure(DocumentLineErrors.LineTypeMismatch(
                    documentId,
                    line.Id,
                    line.LineType,
                    expectedLineType));
            }
        }

        return Result.Success();
    }

    private static async Task<Result> ValidateReversalLinesAsync(
        IApplicationDbContext context,
        Guid reversalDocumentId,
        Guid sourceDocumentId,
        List<DocumentLine> reversalLines,
        CancellationToken cancellationToken)
    {
        List<DocumentLine> sourceLines = await context.DocumentLines
            .AsNoTracking()
            .Where(line => line.DocumentId == sourceDocumentId)
            .ToListAsync(cancellationToken);

        if (sourceLines.Count != reversalLines.Count ||
            reversalLines.Any(line => line.SourceLineId is null) ||
            reversalLines.Select(line => line.SourceLineId).Distinct().Count() != reversalLines.Count)
        {
            return Result.Failure(WarehouseDocumentErrors.ReversalLineMismatch(reversalDocumentId));
        }

        var reversalBySourceLineId = reversalLines.ToDictionary(line => line.SourceLineId!.Value);

        foreach (DocumentLine sourceLine in sourceLines)
        {
            if (!reversalBySourceLineId.TryGetValue(sourceLine.Id, out DocumentLine? reversalLine) ||
                !IsExactCopy(sourceLine, reversalLine))
            {
                return Result.Failure(WarehouseDocumentErrors.ReversalLineMismatch(reversalDocumentId));
            }
        }

        return Result.Success();
    }

    private static bool IsExactCopy(DocumentLine source, DocumentLine reversal) =>
        source.MaterialId == reversal.MaterialId &&
        source.LineType == reversal.LineType &&
        source.Quantity == reversal.Quantity &&
        source.UnitId == reversal.UnitId &&
        source.BaseQuantity == reversal.BaseQuantity &&
        source.UnitPrice == reversal.UnitPrice &&
        source.BatchNumber == reversal.BatchNumber &&
        source.ExpiryDate == reversal.ExpiryDate;
}
