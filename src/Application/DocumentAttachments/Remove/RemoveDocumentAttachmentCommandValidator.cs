using FluentValidation;

namespace Application.DocumentAttachments.Remove;

internal sealed class RemoveDocumentAttachmentCommandValidator : AbstractValidator<RemoveDocumentAttachmentCommand>
{
    public RemoveDocumentAttachmentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.AttachmentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
