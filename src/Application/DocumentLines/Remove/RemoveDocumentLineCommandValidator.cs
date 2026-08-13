using FluentValidation;

namespace Application.DocumentLines.Remove;

internal sealed class RemoveDocumentLineCommandValidator : AbstractValidator<RemoveDocumentLineCommand>
{
    public RemoveDocumentLineCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.LineId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
