namespace Application.Abstractions.PolymorphicReferences;

public interface IPolymorphicReferenceAuditor
{
    Task<PolymorphicReferenceAuditResult> AuditAsync(
        int maximumFindings,
        CancellationToken cancellationToken = default);
}
