using FluentValidation;

namespace Application.WarehouseDocuments.Post;

internal sealed class PostDocumentCommandValidator : AbstractValidator<PostDocumentCommand>
{
    public PostDocumentCommandValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
        RuleFor(c => c.IdempotencyKey)
            .NotEqual(Guid.Empty)
            .When(c => c.IdempotencyKey.HasValue);
    }
}
