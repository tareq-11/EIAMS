using Application.Abstractions.Messaging;

namespace Application.Returns.GetEligibleItems;

public sealed record GetReturnEligibleItemsQuery(Guid OriginalIssueDocumentId)
    : IQuery<IReadOnlyList<ReturnEligibleItemResponse>>;
