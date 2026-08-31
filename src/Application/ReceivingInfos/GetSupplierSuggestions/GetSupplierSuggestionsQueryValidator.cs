using FluentValidation;

namespace Application.ReceivingInfos.GetSupplierSuggestions;

internal sealed class GetSupplierSuggestionsQueryValidator : AbstractValidator<GetSupplierSuggestionsQuery>
{
    public GetSupplierSuggestionsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(200);
    }
}
