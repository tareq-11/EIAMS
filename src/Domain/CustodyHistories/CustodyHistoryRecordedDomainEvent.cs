using Domain.Common;
using SharedKernel;

namespace Domain.CustodyHistories;

public sealed record CustodyHistoryRecordedDomainEvent(
    Guid HistoryId,
    Guid CustodyId,
    CustodyStatus FromStatus,
    CustodyStatus ToStatus) : IDomainEvent;
