using SharedKernel;

namespace Domain.ReturnInfos;

public sealed record ReturnInfoCreatedDomainEvent(Guid DocumentId, Guid OriginalIssueDocumentId) : IDomainEvent;
