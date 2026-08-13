using FluentValidation;

namespace Application.DocumentAttachments.Upload;

internal sealed class UploadDocumentAttachmentCommandValidator : AbstractValidator<UploadDocumentAttachmentCommand>
{
    public UploadDocumentAttachmentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.AttachmentType).IsInEnum();
        RuleFor(c => c.OriginalFilename).NotEmpty();
        RuleFor(c => c.MimeType).NotEmpty();
        RuleFor(c => c.ContentLength).GreaterThan(0);
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
