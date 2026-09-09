using Application.Abstractions.Filtering;

namespace Application.UnitTests.Filtering;

public sealed class DateRangeLimitsTests
{
    [Fact]
    public void DateTimeOffsetRange_ShouldApplyTheBoundedHalfOpenContract()
    {
        DateTimeOffset from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddDays(366);

        DateRangeLimits.IsValid(null, null).ShouldBeTrue();
        DateRangeLimits.IsValid(from, null).ShouldBeFalse();
        DateRangeLimits.IsValid(null, to).ShouldBeFalse();
        DateRangeLimits.IsValid(to, from).ShouldBeFalse();
        DateRangeLimits.IsValid(from, from).ShouldBeFalse();
        DateRangeLimits.IsValid(from, to).ShouldBeTrue();
        DateRangeLimits.IsValid(from, to.AddTicks(1)).ShouldBeFalse();
    }

    [Fact]
    public void DateTimeRange_ShouldApplyTheBoundedHalfOpenContract()
    {
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddDays(366);

        DateRangeLimits.IsValid(null, null).ShouldBeTrue();
        DateRangeLimits.IsValid(from, null).ShouldBeFalse();
        DateRangeLimits.IsValid(null, to).ShouldBeFalse();
        DateRangeLimits.IsValid(to, from).ShouldBeFalse();
        DateRangeLimits.IsValid(from, from).ShouldBeFalse();
        DateRangeLimits.IsValid(from, to).ShouldBeTrue();
        DateRangeLimits.IsValid(from, to.AddTicks(1)).ShouldBeFalse();
    }
}
