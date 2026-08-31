using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.InventoryBalances.GetList;

public sealed class GetInventoryBalancesQueryValidator : AbstractValidator<GetInventoryBalancesQuery>
{
    public GetInventoryBalancesQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.MaterialId).NotEqual(Guid.Empty).When(query => query.MaterialId.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
