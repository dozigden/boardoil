using BoardOil.Abstractions.Jobs;
using Cronos;

namespace BoardOil.Services.Jobs;

public sealed class CronOccurrenceCalculator : ICronOccurrenceCalculator
{
    public DateTime? GetLatestOccurrence(
        string cronExpression, TimeZoneInfo timeZone, DateTime fromUtcExclusive, DateTime throughUtcInclusive) =>
        Latest(Parse(cronExpression), timeZone, EnsureUtc(fromUtcExclusive), EnsureUtc(throughUtcInclusive));

    public DateTime? GetNextOccurrence(string cronExpression, TimeZoneInfo timeZone, DateTime afterUtc) =>
        Parse(cronExpression).GetNextOccurrence(EnsureUtc(afterUtc), timeZone);

    public void Validate(string cronExpression) =>
        _ = Parse(cronExpression).GetNextOccurrence(DateTime.UnixEpoch, TimeZoneInfo.Utc);

    private static DateTime? Latest(
        CronExpression expression, TimeZoneInfo timeZone, DateTime fromUtcExclusive, DateTime throughUtcInclusive)
    {
        if (throughUtcInclusive <= fromUtcExclusive)
        {
            return null;
        }

        var latest = expression.GetPreviousOccurrence(throughUtcInclusive, timeZone, inclusive: true);
        return latest > fromUtcExclusive ? latest : null;
    }

    private static CronExpression Parse(string cronExpression) =>
        CronExpression.Parse(cronExpression, CronFormat.Standard);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
