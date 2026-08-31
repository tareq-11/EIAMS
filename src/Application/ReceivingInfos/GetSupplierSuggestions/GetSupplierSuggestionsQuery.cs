using Application.Abstractions.Messaging;

namespace Application.ReceivingInfos.GetSupplierSuggestions;

public sealed record GetSupplierSuggestionsQuery(string? Search) : IQuery<IReadOnlyList<string>>;
