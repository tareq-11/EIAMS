using FluentValidation;

namespace Application.WarehouseDocuments.Reject;

internal sealed class RejectDocumentCommandValidator : AbstractValidator<RejectDocumentCommand>
{
    public RejectDocumentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
