namespace Infrastructure.Idempotency;

internal sealed class IdempotencyCleanupOptions
{
    internal const string SectionName = "Idempotency:Cleanup";

    public bool Enabled { get; init; } = true;

    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    public int BatchSize { get; init; } = 500;
}
