using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Returns.GetEligibleItems;

public sealed class GetReturnEligibleItemsQueryValidator : AbstractValidator<GetReturnEligibleItemsQuery>
{
    public GetReturnEligibleItemsQueryValidator()
    {
        RuleFor(query => query.OriginalIssueDocumentId).NotEmpty();
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
