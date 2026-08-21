using SharedKernel;

namespace Domain.DocumentLineAssetSelections;

public sealed record DocumentLineAssetSelectedDomainEvent(
    Guid SelectionId,
    Guid DocumentId,
    Guid DocumentLineId,
    Guid AssetId) : IDomainEvent;
