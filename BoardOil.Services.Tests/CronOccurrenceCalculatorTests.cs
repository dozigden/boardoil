using BoardOil.Services.Jobs;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CronOccurrenceCalculatorTests
{
    private readonly CronOccurrenceCalculator _calculator = new();

    [Fact]
    public void Latest_ShouldChooseLastMissedOccurrenceAndHonorBothBoundaries()
    {
        var baseline = new DateTime(2026, 7, 23, 9, 15, 0, DateTimeKind.Utc);
        Assert.Null(_calculator.GetLatestOccurrence("15 * * * *", TimeZoneInfo.Utc,
            baseline, baseline));
        Assert.Equal(new DateTime(2026, 7, 23, 12, 15, 0, DateTimeKind.Utc),
            _calculator.GetLatestOccurrence("15 * * * *", TimeZoneInfo.Utc,
                baseline, new DateTime(2026, 7, 23, 12, 15, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LondonFallback_ShouldRunFixedLocalTimeOnce()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc),
            _calculator.GetLatestOccurrence("30 1 * * *", london,
                new DateTime(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LondonSpringForward_ShouldUseFirstValidLocalTime()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Assert.Equal(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc),
            _calculator.GetNextOccurrence("30 1 * * *", london,
                new DateTime(2026, 3, 28, 2, 0, 0, DateTimeKind.Utc)));
    }
}
