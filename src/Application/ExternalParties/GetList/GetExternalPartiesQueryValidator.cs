using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.ExternalParties.GetList;

internal sealed class GetExternalPartiesQueryValidator : AbstractValidator<GetExternalPartiesQuery>
{
    public GetExternalPartiesQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
