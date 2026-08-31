using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.InventoryAdjustments.GetDisposalEligibleAssets;

public sealed class GetDisposalEligibleAssetsQueryValidator : AbstractValidator<GetDisposalEligibleAssetsQuery>
{
    public GetDisposalEligibleAssetsQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.MaterialId).NotEqual(Guid.Empty).When(query => query.MaterialId.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
