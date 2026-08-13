using FluentValidation;

namespace Application.WarehouseDocuments.Post;

internal sealed class PostDocumentCommandValidator : AbstractValidator<PostDocumentCommand>
{
    public PostDocumentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}
