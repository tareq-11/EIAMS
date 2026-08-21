using FluentValidation;

namespace Application.DocumentLineAssetSelections.Add;

internal sealed class AddDocumentLineAssetSelectionCommandValidator
    : AbstractValidator<AddDocumentLineAssetSelectionCommand>
{
    public AddDocumentLineAssetSelectionCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.AssetId).NotEmpty();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
