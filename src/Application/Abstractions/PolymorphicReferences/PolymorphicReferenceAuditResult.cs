namespace Application.Abstractions.PolymorphicReferences;

public sealed record PolymorphicReferenceAuditResult(
    int TotalFindings,
    IReadOnlyList<PolymorphicReferenceFinding> Findings,
    bool SkippedBecauseLockUnavailable);

public sealed record PolymorphicReferenceFinding(
    string SourceType,
    Guid SourceId,
    string PartyType,
    Guid PartyId,
    string LifecycleStatus,
    string Reason);
