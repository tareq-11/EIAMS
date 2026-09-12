using Application.Abstractions.Filtering;
using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Reports.Documents;

public sealed class GetDocumentsReportQueryValidator : AbstractValidator<GetDocumentsReportQuery>
{
    public GetDocumentsReportQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.DocumentType).IsInEnum().When(query => query.DocumentType.HasValue);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query)
            .Must(query => DateRangeLimits.IsValid(query.FromUtc, query.ToUtc))
            .WithMessage("FromUtc and ToUtc must form a half-open range no longer than 366 days.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
