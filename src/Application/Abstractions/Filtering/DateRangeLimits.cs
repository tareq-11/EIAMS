namespace Application.Abstractions.Filtering;

/// <summary>
/// Defines the bounded, half-open date ranges accepted by read endpoints.
/// </summary>
public static class DateRangeLimits
{
    /// <summary>
    /// The longest date range a single synchronous read request may query.
    /// </summary>
    public static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);

    /// <summary>
    /// Returns whether the supplied UTC instants form a bounded half-open range.
    /// </summary>
    public static bool IsValid(DateTimeOffset? fromUtc, DateTimeOffset? toUtc) =>
        fromUtc.HasValue == toUtc.HasValue &&
        (!fromUtc.HasValue ||
         fromUtc.Value < toUtc!.Value && toUtc.Value - fromUtc.Value <= MaximumRange);

    /// <summary>
    /// Returns whether the supplied UTC date-times form a bounded half-open range.
    /// </summary>
    public static bool IsValid(DateTime? fromUtc, DateTime? toUtc) =>
        fromUtc.HasValue == toUtc.HasValue &&
        (!fromUtc.HasValue ||
         fromUtc.Value < toUtc!.Value && toUtc.Value - fromUtc.Value <= MaximumRange);
}
