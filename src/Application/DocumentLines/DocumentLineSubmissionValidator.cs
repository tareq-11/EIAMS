using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Posting;
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

public static class DocumentLineSubmissionValidator
{
    public static Task<Result> ValidateAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        AssetCreationOptions assetCreationOptions,
        IEnumerable<IDocumentSubmissionValidator> typeValidators,
        CancellationToken cancellationToken) =>
        ValidateAsync(context, document, null, assetCreationOptions, typeValidators, cancellationToken);

    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        IReadOnlyList<DocumentLine>? preloadedLines,
        AssetCreationOptions assetCreationOptions,
        IEnumerable<IDocumentSubmissionValidator> typeValidators,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentLine> lines = preloadedLines ?? await context.DocumentLines
            .AsNoTracking()
            .Where(line => line.DocumentId == document.Id)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return Result.Failure(WarehouseDocumentErrors.LinesRequired(document.Id));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return await ValidateReversalLinesAsync(
                context,
                document.Id,
                document.ReversalOfDocumentId.Value,
                lines,
                cancellationToken);
        }

        Result documentLimitResult = DocumentAssetLimitRules.Validate(
            document.Id,
            lines.Count,
            lines.Where(line => line.LineType == DocumentLineType.Asset).Sum(line => line.BaseQuantity),
            assetCreationOptions);

        if (documentLimitResult.IsFailure)
        {
            return documentLimitResult;
        }

        Result operationalResult = await ValidateOperationalLinesAsync(
            context,
            document,
            lines,
            assetCreationOptions.MaxAssetsPerLine,
            cancellationToken);

        if (operationalResult.IsFailure)
        {
            return operationalResult;
        }

        IDocumentSubmissionValidator? typeValidator = typeValidators
            .SingleOrDefault(validator => validator.DocumentType == document.DocumentType);

        return typeValidator is null
            ? Result.Success()
            : await typeValidator.ValidateAsync(document, lines, cancellationToken);
    }

    private static async Task<Result> ValidateOperationalLinesAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        int maxAssetsPerLine,
        CancellationToken cancellationToken)
    {
        Guid[] materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();

        var catalogRows = await (
            from material in context.Materials.AsNoTracking()
            where materialIds.Contains(material.Id)
            join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id into fGroup
            from family in fGroup.DefaultIfEmpty()
            join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id into cGroup
            from category in cGroup.DefaultIfEmpty()
            join domain in context.MaterialDomains.AsNoTracking() on category.MaterialDomainId equals domain.Id into dGroup
            from domain in dGroup.DefaultIfEmpty()
            select new
            {
                Material = material,
                Family = family,
                Category = category,
                Domain = domain
            }
        ).ToListAsync(cancellationToken);

        var rowByMaterialId = catalogRows.ToDictionary(r => r.Material.Id);

        Guid[] unitIds = catalogRows
            .Where(r => r.Family is not null)
            .Select(r => r.Family!.BaseUnitId)
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
            if (!rowByMaterialId.TryGetValue(line.MaterialId, out var row))
            {
                return Result.Failure(MaterialErrors.NotFound(line.MaterialId));
            }

            if (row.Material.Status != MaterialStatus.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialNotActive(row.Material.Id));
            }

            if (row.Family is null)
            {
                return Result.Failure(MaterialFamilyErrors.NotFound(row.Material.FamilyId));
            }

            if (row.Family.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialFamilyNotActive(row.Family.Id));
            }

            if (row.Category is null)
            {
                return Result.Failure(MaterialCategoryErrors.NotFound(row.Family.CategoryId));
            }

            if (row.Category.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialCategoryNotActive(row.Category.Id));
            }

            if (row.Domain is null)
            {
                return Result.Failure(MaterialDomainErrors.NotFound(row.Category.MaterialDomainId));
            }

            if (row.Domain.Status != Status.Active)
            {
                return Result.Failure(DocumentLineErrors.MaterialDomainNotActive(row.Domain.Id));
            }

            if (!existingUnitIds.Contains(row.Family.BaseUnitId))
            {
                return Result.Failure(DocumentLineErrors.UnitNotFound(row.Family.BaseUnitId));
            }

            MaterialUnitConversion? conversion = null;

            if (line.UnitId is not null && line.UnitId != row.Family.BaseUnitId)
            {
                if (!existingUnitIds.Contains(line.UnitId.Value))
                {
                    return Result.Failure(DocumentLineErrors.UnitNotFound(line.UnitId.Value));
                }

                if (!conversionByMaterialAndUnit.TryGetValue(
                        (line.MaterialId, line.UnitId.Value),
                        out conversion) ||
                    conversion.ToBaseUnitId != row.Family.BaseUnitId)
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
                row.Family.BaseUnitId,
                conversion);

            if (baseQuantityResult.IsFailure)
            {
                return Result.Failure(baseQuantityResult.Error);
            }

            if (line.BaseQuantity != baseQuantityResult.Value)
            {
                return Result.Failure(DocumentLineErrors.BaseQuantityMismatch(
                    document.Id,
                    line.Id,
                    line.BaseQuantity,
                    baseQuantityResult.Value));
            }

            DocumentLineType expectedLineType = row.Material.IsAssetTracked
                ? DocumentLineType.Asset
                : DocumentLineType.Normal;

            if (line.LineType != expectedLineType)
            {
                return Result.Failure(DocumentLineErrors.LineTypeMismatch(
                    document.Id,
                    line.Id,
                    line.LineType,
                    expectedLineType));
            }

            Result openingTypeResult = OpeningLineRules.Validate(
                document.DocumentType,
                document.Id,
                line.OpeningType);

            if (openingTypeResult.IsFailure)
            {
                return openingTypeResult;
            }

            Result assetQuantityResult = AssetLineRules.Validate(
                line.Id,
                line.LineType,
                line.BaseQuantity,
                maxAssetsPerLine);

            if (assetQuantityResult.IsFailure)
            {
                return assetQuantityResult;
            }
        }

        return Result.Success();
    }

    private static async Task<Result> ValidateReversalLinesAsync(
        IApplicationDbContext context,
        Guid reversalDocumentId,
        Guid sourceDocumentId,
        IReadOnlyList<DocumentLine> reversalLines,
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
        source.ExpiryDate == reversal.ExpiryDate &&
        source.OpeningType == reversal.OpeningType;
}
