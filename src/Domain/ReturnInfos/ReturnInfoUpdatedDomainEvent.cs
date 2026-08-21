using SharedKernel;

namespace Domain.ReturnInfos;

public sealed record ReturnInfoUpdatedDomainEvent(Guid DocumentId, Guid OriginalIssueDocumentId) : IDomainEvent;
