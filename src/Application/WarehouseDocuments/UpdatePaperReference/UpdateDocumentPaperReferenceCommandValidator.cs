using FluentValidation;

namespace Application.WarehouseDocuments.UpdatePaperReference;

internal sealed class UpdateDocumentPaperReferenceCommandValidator
    : AbstractValidator<UpdateDocumentPaperReferenceCommand>
{
    public UpdateDocumentPaperReferenceCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.PaperDocumentNumber).MaximumLength(100);
        RuleFor(c => c.PaperDocumentYear).InclusiveBetween(1900, 9999).When(c => c.PaperDocumentYear is not null);
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
