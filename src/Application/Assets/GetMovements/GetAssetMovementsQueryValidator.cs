using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.Assets.GetMovements;

public sealed class GetAssetMovementsQueryValidator : AbstractValidator<GetAssetMovementsQuery>
{
    public GetAssetMovementsQueryValidator()
    {
        RuleFor(query => query.AssetId).NotEqual(Guid.Empty);
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }
}
