using FluentValidation;

namespace Application.WarehouseDocuments.Create;

internal sealed class CreateWarehouseDocumentCommandValidator : AbstractValidator<CreateWarehouseDocumentCommand>
{
    public CreateWarehouseDocumentCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.DocumentType).IsInEnum();
    }
}
