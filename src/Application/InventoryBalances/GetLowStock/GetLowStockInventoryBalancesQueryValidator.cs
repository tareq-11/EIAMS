using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.InventoryBalances.GetLowStock;

public sealed class GetLowStockInventoryBalancesQueryValidator
    : AbstractValidator<GetLowStockInventoryBalancesQuery>
{
    public GetLowStockInventoryBalancesQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
