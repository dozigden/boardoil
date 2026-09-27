using BoardOil.Services.Jobs;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class DailyOccurrenceCalculatorTests
{
    private readonly DailyOccurrenceCalculator _calculator = new();

    [Fact]
    public void Latest_ShouldChooseLastMissedDayWithoutReplayingEarlierDays()
    {
        var result = _calculator.GetLatestOccurrence(new TimeOnly(3, 0), TimeZoneInfo.Utc,
            Utc(2000, 1, 1, 3), Utc(2026, 9, 27, 12));

        Assert.Equal(Utc(2026, 9, 27, 3), result);
    }

    [Fact]
    public void Latest_ShouldExcludeBaselineAndIncludeEvaluationBoundary()
    {
        var due = Utc(2026, 9, 27, 3);
        var dailyTime = new TimeOnly(3, 0);

        Assert.Null(_calculator.GetLatestOccurrence(dailyTime, TimeZoneInfo.Utc, due, due));
        Assert.Null(_calculator.GetLatestOccurrence(dailyTime, TimeZoneInfo.Utc, due, due.AddHours(1)));
        Assert.Null(_calculator.GetLatestOccurrence(dailyTime, TimeZoneInfo.Utc, due, due.AddTicks(-1)));
        Assert.Equal(due, _calculator.GetLatestOccurrence(dailyTime, TimeZoneInfo.Utc, due.AddTicks(-1), due));
    }

    [Theory]
    [InlineData(2026, 12, 31, 2027, 1, 1)]
    [InlineData(2028, 2, 28, 2028, 2, 29)]
    [InlineData(2028, 2, 29, 2028, 3, 1)]
    public void Next_ShouldAdvanceOneLocalCalendarDayAtExactBoundary(int year, int month, int day, int nextYear, int nextMonth, int nextDay)
    {
        var result = _calculator.GetNextOccurrence(new TimeOnly(3, 0), TimeZoneInfo.Utc, Utc(year, month, day, 3));

        Assert.Equal(Utc(nextYear, nextMonth, nextDay, 3), result);
    }

    [Fact]
    public void Next_ShouldReturnTodaysOccurrenceWhenStillAhead()
    {
        Assert.Equal(Utc(2026, 9, 27, 3), _calculator.GetNextOccurrence(new TimeOnly(3, 0), TimeZoneInfo.Utc,
            Utc(2026, 9, 27, 3).AddTicks(-1)));
    }

    [Fact]
    public void LondonFallback_ShouldRunFixedLocalTimeOnceAtEarlierInstant()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var dailyTime = new TimeOnly(1, 30);
        var first = Utc(2026, 10, 25, 0, 30);

        Assert.Equal(first, _calculator.GetNextOccurrence(dailyTime, london, Utc(2026, 10, 24, 23)));
        Assert.Equal(first, _calculator.GetLatestOccurrence(dailyTime, london, Utc(2026, 10, 24, 23), Utc(2026, 10, 25, 2)));
        Assert.Null(_calculator.GetLatestOccurrence(dailyTime, london, first, Utc(2026, 10, 25, 2)));
        Assert.Equal(Utc(2026, 10, 26, 1, 30), _calculator.GetNextOccurrence(dailyTime, london, first));
        Assert.Equal(Utc(2026, 10, 26, 1, 30), _calculator.GetNextOccurrence(dailyTime, london, Utc(2026, 10, 25, 1, 15)));
    }

    [Fact]
    public void LondonSpringForward_ShouldUseFirstValidLocalTime()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var dailyTime = new TimeOnly(1, 30, 12).Add(TimeSpan.FromTicks(1234));
        var due = Utc(2026, 3, 29, 1);

        Assert.Equal(due, _calculator.GetNextOccurrence(dailyTime, london, Utc(2026, 3, 28, 2)));
        Assert.Equal(due, _calculator.GetLatestOccurrence(dailyTime, london, due.AddTicks(-1), due));
        Assert.Null(_calculator.GetLatestOccurrence(dailyTime, london, due, due.AddHours(1)));
    }

    [Theory]
    [InlineData(2026, 3, 28, 3, 2026, 3, 29, 2)]
    [InlineData(2026, 10, 24, 2, 2026, 10, 25, 3)]
    public void DailyMaintenance_ShouldFollowLocalTimeAcrossDst(int year, int month, int day, int hour, int nextYear, int nextMonth, int nextDay, int nextHour)
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        Assert.Equal(Utc(nextYear, nextMonth, nextDay, nextHour),
            _calculator.GetNextOccurrence(new TimeOnly(3, 0), london, Utc(year, month, day, hour)));
    }

    [Fact]
    public void NonHourOffset_ShouldUseLocalDateInsteadOfUtcDate()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+0545", TimeSpan.FromMinutes(345), "Test", "Test");
        var after = Utc(2026, 9, 26, 22);

        Assert.Equal(Utc(2026, 9, 27, 21, 15), _calculator.GetNextOccurrence(new TimeOnly(3, 0), zone, after));
        Assert.Equal(Utc(2026, 9, 26, 21, 15), _calculator.GetLatestOccurrence(new TimeOnly(3, 0), zone, after.AddDays(-1), after));
    }

    [Fact]
    public void HalfHourSpringGap_ShouldShiftToItsExactEnd()
    {
        var zone = HalfHourZone();
        var after = Utc(2026, 3, 1, 0);

        Assert.Equal(Utc(2026, 3, 1, 2), _calculator.GetNextOccurrence(new TimeOnly(2, 15, 19), zone, after));
    }

    [Fact]
    public void HalfHourAutumnOverlap_ShouldNotRepeat()
    {
        var zone = HalfHourZone();
        var first = Utc(2026, 10, 1, 1, 15);

        Assert.Equal(first, _calculator.GetNextOccurrence(new TimeOnly(1, 45), zone, Utc(2026, 10, 1, 0)));
        Assert.Equal(Utc(2026, 10, 2, 1, 45), _calculator.GetNextOccurrence(new TimeOnly(1, 45), zone, first));
    }

    [Fact]
    public void Latest_BeforeTodaysDueTime_ShouldUsePreviousLocalDate()
    {
        Assert.Equal(Utc(2026, 9, 26, 3), _calculator.GetLatestOccurrence(new TimeOnly(3, 0), TimeZoneInfo.Utc,
            Utc(2026, 9, 25, 12), Utc(2026, 9, 27, 2)));
    }

    [Fact]
    public void Occurrence_ShouldPreserveSecondsOutsideDstGap()
    {
        var dailyTime = new TimeOnly(3, 15, 42).Add(TimeSpan.FromTicks(4567));
        var expected = Utc(2026, 9, 27, 3, 15).AddSeconds(42).AddTicks(4567);

        Assert.Equal(expected, _calculator.GetNextOccurrence(dailyTime, TimeZoneInfo.Utc, Utc(2026, 9, 27, 0)));
    }

    private static TimeZoneInfo HalfHourZone()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 1);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            TimeSpan.FromMinutes(30), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("TestHalfHour", TimeSpan.Zero, "Test", "Test", "Test DST", [rule]);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
