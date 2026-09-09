using Application.Abstractions.Filtering;
using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Reports.CountAdjustments;

public sealed class GetCountAdjustmentsReportQueryValidator : AbstractValidator<GetCountAdjustmentsReportQuery>
{
    public GetCountAdjustmentsReportQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query)
            .Must(query => DateRangeLimits.IsValid(query.FromUtc, query.ToUtc))
            .WithMessage("FromUtc and ToUtc must form a half-open range no longer than 366 days.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
