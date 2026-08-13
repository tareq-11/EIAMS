using FluentValidation;

namespace Application.WarehouseDocuments.ReturnToDraft;

internal sealed class ReturnDocumentToDraftCommandValidator : AbstractValidator<ReturnDocumentToDraftCommand>
{
    public ReturnDocumentToDraftCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
