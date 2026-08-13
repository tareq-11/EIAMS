using SharedKernel;

namespace Domain.TransferInfos;

public sealed record TransferInfoUpdatedDomainEvent(Guid DocumentId, Guid DestinationWarehouseId) : IDomainEvent;
