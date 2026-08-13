using FluentValidation;

namespace Application.WarehouseDocuments.CreateReversal;

internal sealed class CreateReversalDocumentCommandValidator : AbstractValidator<CreateReversalDocumentCommand>
{
    public CreateReversalDocumentCommandValidator()
    {
        RuleFor(c => c.SourceDocumentId).NotEmpty();
    }
}
