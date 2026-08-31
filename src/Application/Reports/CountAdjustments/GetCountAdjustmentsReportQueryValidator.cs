using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Reports.CountAdjustments;

public sealed class GetCountAdjustmentsReportQueryValidator : AbstractValidator<GetCountAdjustmentsReportQuery>
{
    public GetCountAdjustmentsReportQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query)
            .Must(query => !query.FromUtc.HasValue || !query.ToUtc.HasValue || query.FromUtc < query.ToUtc)
            .WithMessage("FromUtc must be earlier than ToUtc.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
