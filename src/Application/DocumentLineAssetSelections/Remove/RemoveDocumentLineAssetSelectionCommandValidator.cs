using FluentValidation;

namespace Application.DocumentLineAssetSelections.Remove;

internal sealed class RemoveDocumentLineAssetSelectionCommandValidator
    : AbstractValidator<RemoveDocumentLineAssetSelectionCommand>
{
    public RemoveDocumentLineAssetSelectionCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.AssetId).NotEmpty();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
