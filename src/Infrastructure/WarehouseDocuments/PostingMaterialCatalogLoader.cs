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

        List<Material> materials = await context.Materials
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync(cancellationToken);
        var materialById = materials.ToDictionary(material => material.Id);

        Guid[] familyIds = materials.Select(material => material.FamilyId).Distinct().ToArray();
        List<MaterialFamily> families = await context.MaterialFamilies
            .Where(family => familyIds.Contains(family.Id))
            .ToListAsync(cancellationToken);
        var familyById = families.ToDictionary(family => family.Id);

        Guid[] categoryIds = families.Select(family => family.CategoryId).Distinct().ToArray();
        List<MaterialCategory> categories = await context.MaterialCategories
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
        var categoryById = categories.ToDictionary(category => category.Id);

        Guid[] domainIds = categories.Select(category => category.MaterialDomainId).Distinct().ToArray();
        List<MaterialDomain> domains = await context.MaterialDomains
            .Where(domain => domainIds.Contains(domain.Id))
            .ToListAsync(cancellationToken);
        var domainById = domains.ToDictionary(domain => domain.Id);

        var result = new Dictionary<Guid, PostingMaterialInfo>();

        foreach (DocumentLine line in lines)
        {
            if (!materialById.TryGetValue(line.MaterialId, out Material? material))
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialErrors.NotFound(line.MaterialId));
            }

            if (material.Status != MaterialStatus.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialNotActive(material.Id));
            }

            if (!familyById.TryGetValue(material.FamilyId, out MaterialFamily? family))
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialFamilyErrors.NotFound(material.FamilyId));
            }

            if (family.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialFamilyNotActive(family.Id));
            }

            if (!categoryById.TryGetValue(family.CategoryId, out MaterialCategory? category))
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialCategoryErrors.NotFound(family.CategoryId));
            }

            if (category.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialCategoryNotActive(category.Id));
            }

            if (!domainById.TryGetValue(category.MaterialDomainId, out MaterialDomain? domain))
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    MaterialDomainErrors.NotFound(category.MaterialDomainId));
            }

            if (domain.Status != Status.Active)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, PostingMaterialInfo>>(
                    DocumentLineErrors.MaterialDomainNotActive(domain.Id));
            }

            DocumentLineType expectedLineType = material.IsAssetTracked
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

            result[material.Id] = new PostingMaterialInfo(material, domain.Id);
        }

        return result;
    }
}
