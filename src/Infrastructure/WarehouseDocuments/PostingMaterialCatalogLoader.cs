using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed record PostingMaterialInfo(Material Material, Guid MaterialDomainId);

internal static class PostingMaterialCatalogLoader
{
    public static async Task<Result<IReadOnlyDictionary<Guid, PostingMaterialInfo>>> LoadAsync(
        IApplicationDbContext context,
        Guid documentId,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        Guid[] materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();

        var rows = await (
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

        var rowByMaterialId = rows.ToDictionary(r => r.Material.Id);

        var result = new Dictionary<Guid, PostingMaterialInfo>();

        foreach (DocumentLine line in lines)
        {
            if (!rowByMaterialId.TryGetValue(line.MaterialId, out var row))
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialErrors.NotFound(line.MaterialId));
            }

            if (row.Material.Status != MaterialStatus.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialNotActive(row.Material.Id));
            }

            if (row.Family is null)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialFamilyErrors.NotFound(row.Material.FamilyId));
            }

            if (row.Family.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialFamilyNotActive(row.Family.Id));
            }

            if (row.Category is null)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialCategoryErrors.NotFound(row.Family.CategoryId));
            }

            if (row.Category.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialCategoryNotActive(row.Category.Id));
            }

            if (row.Domain is null)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialDomainErrors.NotFound(row.Category.MaterialDomainId));
            }

            if (row.Domain.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialDomainNotActive(row.Domain.Id));
            }

            DocumentLineType expectedLineType = row.Material.IsAssetTracked
                ? DocumentLineType.Asset
                : DocumentLineType.Normal;

            if (line.LineType != expectedLineType)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.LineTypeMismatch(
                        documentId,
                        line.Id,
                        line.LineType,
                        expectedLineType));
            }

            result[row.Material.Id] = new PostingMaterialInfo(row.Material, row.Domain.Id);
        }

        return result;
    }
}
