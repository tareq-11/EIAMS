using FluentValidation;

namespace Application.WarehouseDocuments.Cancel;

internal sealed class CancelDocumentCommandValidator : AbstractValidator<CancelDocumentCommand>
{
    public CancelDocumentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
        RuleFor(c => c.Reason).MaximumLength(1000);
    }
}
