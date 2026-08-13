using FluentValidation;

namespace Application.DocumentLines.Add;

internal sealed class AddDocumentLineCommandValidator : AbstractValidator<AddDocumentLineCommand>
{
    public AddDocumentLineCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.MaterialId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0);
        RuleFor(c => c.Quantity).PrecisionScale(18, 3, false);
        RuleFor(c => c.UnitPrice).GreaterThanOrEqualTo(0).When(c => c.UnitPrice is not null);
        RuleFor(c => c.UnitPrice!.Value).PrecisionScale(18, 2, false).When(c => c.UnitPrice is not null);
        RuleFor(c => c.BatchNumber).MaximumLength(100);
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
