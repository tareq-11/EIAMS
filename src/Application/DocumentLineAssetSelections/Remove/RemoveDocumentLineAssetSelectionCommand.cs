using Application.Abstractions.Messaging;

namespace Application.DocumentLineAssetSelections.Remove;

public sealed record RemoveDocumentLineAssetSelectionCommand(
    Guid DocumentId,
    Guid LineId,
    Guid AssetId,
    int ExpectedRowVersion) : ICommand;
