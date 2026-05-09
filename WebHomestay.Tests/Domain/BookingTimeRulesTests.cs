// WebHomestay.Tests/Domain/BookingTimeRulesTests.cs
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Domain;

public class BookingTimeRulesTests
{
    [Fact]
    public void BuildDailyStay_UsesHotelCheckInAndCheckOutTimes()
    {
        var interval = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));

        Assert.Equal(new DateTime(2026, 5, 9, 14, 0, 0), interval.Start);
        Assert.Equal(new DateTime(2026, 5, 11, 12, 0, 0), interval.End);
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenHourlySlotFallsInsideDailyStay()
    {
        var daily = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));
        var hourlyStart = new DateTime(2026, 5, 10, 9, 30, 0);
        var hourlyEnd = new DateTime(2026, 5, 10, 11, 30, 0);

        var overlaps = BookingTimeRules.Overlaps(
            daily.Start,
            daily.End,
            hourlyStart,
            hourlyEnd);

        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenIntervalsOnlyTouchAtBoundary()
    {
        var leftEnd = new DateTime(2026, 5, 10, 12, 0, 0);
        var rightStart = new DateTime(2026, 5, 10, 12, 0, 0);

        var overlaps = BookingTimeRules.Overlaps(
            new DateTime(2026, 5, 10, 9, 0, 0),
            leftEnd,
            rightStart,
            new DateTime(2026, 5, 10, 14, 0, 0));

        Assert.False(overlaps);
    }
}
