using Application.Abstractions.Messaging;

namespace Application.DocumentLineAssetSelections.Add;

public sealed record AddDocumentLineAssetSelectionCommand(
    Guid DocumentId,
    Guid LineId,
    Guid AssetId,
    int ExpectedRowVersion) : ICommand<Guid>;
