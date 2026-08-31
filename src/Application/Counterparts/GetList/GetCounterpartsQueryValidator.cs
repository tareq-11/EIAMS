using FluentValidation;

namespace Application.Counterparts.GetList;

internal sealed class GetCounterpartsQueryValidator : AbstractValidator<GetCounterpartsQuery>
{
    public GetCounterpartsQueryValidator()
    {
        RuleFor(query => query.Type)
            .Must(type => type is null || Enum.IsDefined(type.Value))
            .WithMessage("Type must be a known counterpart type.");
        RuleFor(query => query.Search).MaximumLength(200);
    }
}
