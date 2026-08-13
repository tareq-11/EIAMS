using FluentValidation;

namespace Application.DocumentLines.Update;

internal sealed class UpdateDocumentLineCommandValidator : AbstractValidator<UpdateDocumentLineCommand>
{
    public UpdateDocumentLineCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.LineId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0);
        RuleFor(c => c.Quantity).PrecisionScale(18, 3, false);
        RuleFor(c => c.UnitPrice).GreaterThanOrEqualTo(0).When(c => c.UnitPrice is not null);
        RuleFor(c => c.UnitPrice!.Value).PrecisionScale(18, 2, false).When(c => c.UnitPrice is not null);
        RuleFor(c => c.BatchNumber).MaximumLength(100);
        RuleFor(c => c.OpeningType)
            .Must(openingType => openingType is null || Enum.IsDefined(openingType.Value))
            .WithMessage("OpeningType must be a known value.");
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
