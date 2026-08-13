using Application.Abstractions.Ledger;

namespace Application.Abstractions.Posting;

/// <summary>The immutable set of ledger movements a posting strategy wants applied.</summary>
public sealed record PostingPlan(IReadOnlyList<MovementDraft> Movements);
