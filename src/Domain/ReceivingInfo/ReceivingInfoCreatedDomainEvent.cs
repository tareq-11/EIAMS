using Domain.Common;
using SharedKernel;

namespace Domain.ReceivingInfos;

public sealed record ReceivingInfoCreatedDomainEvent(Guid DocumentId, ReceivingType ReceivingType) : IDomainEvent;
