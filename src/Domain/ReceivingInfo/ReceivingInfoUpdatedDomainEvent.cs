using Domain.Common;
using SharedKernel;

namespace Domain.ReceivingInfos;

public sealed record ReceivingInfoUpdatedDomainEvent(Guid DocumentId, ReceivingType ReceivingType) : IDomainEvent;
