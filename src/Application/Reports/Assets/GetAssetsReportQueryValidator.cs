using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Reports.Assets;

public sealed class GetAssetsReportQueryValidator : AbstractValidator<GetAssetsReportQuery>
{
    public GetAssetsReportQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
