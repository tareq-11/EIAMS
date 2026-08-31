using FluentValidation;

namespace Application.Custodies.Transfer;

public sealed class TransferCustodyCommandValidator : AbstractValidator<TransferCustodyCommand>
{
    public TransferCustodyCommandValidator()
    {
        RuleFor(c => c.CustodyId).NotEmpty();
        RuleFor(c => c.NewHolderId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
        RuleFor(c => c.Note).MaximumLength(300).When(c => c.Note is not null);
    }
}
