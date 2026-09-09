using Application.Abstractions.Filtering;
using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.WarehouseDocuments.GetList;

public sealed class GetWarehouseDocumentsQueryValidator : AbstractValidator<GetWarehouseDocumentsQuery>
{
    public GetWarehouseDocumentsQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.SiteId).NotEqual(Guid.Empty).When(query => query.SiteId.HasValue);
        RuleFor(query => query.DocumentType).IsInEnum().When(query => query.DocumentType.HasValue);
        RuleFor(query => query.DocumentStatus).IsInEnum().When(query => query.DocumentStatus.HasValue);
        RuleFor(query => query.SystemReferenceNumber).MaximumLength(200);
        RuleFor(query => query.PaperDocumentNumber).MaximumLength(200);
        RuleFor(query => query)
            .Must(query => DateRangeLimits.IsValid(query.FromDateUtc, query.ToDateUtc))
            .WithMessage("FromDateUtc and ToDateUtc must form a half-open range no longer than 366 days.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
