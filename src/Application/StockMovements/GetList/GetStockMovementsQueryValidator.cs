using Application.Abstractions.Filtering;
using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.StockMovements.GetList;

public sealed class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
{
    public GetStockMovementsQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.MaterialId).NotEqual(Guid.Empty).When(query => query.MaterialId.HasValue);
        RuleFor(query => query.DocumentId).NotEqual(Guid.Empty).When(query => query.DocumentId.HasValue);
        RuleFor(query => query.MovementType).IsInEnum().When(query => query.MovementType.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query)
            .Must(query => DateRangeLimits.IsValid(query.FromUtc, query.ToUtc))
            .WithMessage("FromUtc and ToUtc must form a half-open range no longer than 366 days.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
