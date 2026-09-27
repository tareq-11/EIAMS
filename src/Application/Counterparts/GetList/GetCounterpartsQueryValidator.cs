using FluentValidation;
using Domain.Common;

namespace Application.Counterparts.GetList;

internal sealed class GetCounterpartsQueryValidator : AbstractValidator<GetCounterpartsQuery>
{
    public GetCounterpartsQueryValidator()
    {
        RuleFor(query => query.Operation)
            .Must(operation => operation is OperationType.Receiving or OperationType.Issue
                or OperationType.Transfer or OperationType.Return)
            .WithMessage("Operation must support counterpart selection.");
        RuleFor(query => query.Type)
            .Must(type => type is null || Enum.IsDefined(type.Value))
            .WithMessage("Type must be a known counterpart type.");
        RuleFor(query => query)
            .Must(query => query.Operation != OperationType.Receiving ||
                query.Type is null or PartyType.External)
            .WithMessage("Receiving counterparts must be External parties.");
        RuleFor(query => query)
            .Must(query => query.Operation != OperationType.Issue ||
                query.Type is null or PartyType.Employee or PartyType.OrganizationalUnit or PartyType.Site)
            .WithMessage("Issue counterparts must be internal parties.");
        RuleFor(query => query.Search).MaximumLength(200);
    }
}
