namespace Infrastructure.PolymorphicReferences;

public sealed class PolymorphicReferenceAuditOptions
{
    public const string SectionName = "PolymorphicReferenceAudit";

    public bool Enabled { get; init; } = true;

    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan Interval { get; init; } = TimeSpan.FromDays(1);

    public int MaximumLoggedFindingsPerCycle { get; init; } = 100;

    public int AdvisoryLockKey { get; init; } = 90_421_001;
}
