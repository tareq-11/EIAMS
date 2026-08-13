using SharedKernel;

namespace Domain.TransferInfos;

public sealed record TransferInfoCreatedDomainEvent(Guid DocumentId, Guid DestinationWarehouseId) : IDomainEvent;
