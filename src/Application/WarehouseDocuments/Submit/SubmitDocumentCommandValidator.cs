using FluentValidation;

namespace Application.WarehouseDocuments.Submit;

internal sealed class SubmitDocumentCommandValidator : AbstractValidator<SubmitDocumentCommand>
{
    public SubmitDocumentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
