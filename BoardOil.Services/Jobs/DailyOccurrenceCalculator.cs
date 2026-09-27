using BoardOil.Abstractions.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class DailyOccurrenceCalculator : IDailyOccurrenceCalculator
{
    public DateTime? GetLatestOccurrence(
        TimeOnly dailyTime, TimeZoneInfo timeZone, DateTime fromUtcExclusive, DateTime throughUtcInclusive)
    {
        var fromUtc = EnsureUtc(fromUtcExclusive);
        var throughUtc = EnsureUtc(throughUtcInclusive);
        if (throughUtc <= fromUtc) return null;

        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(throughUtc, timeZone));
        while (true)
        {
            var occurrence = GetOccurrence(date, dailyTime, timeZone);
            if (occurrence <= throughUtc) return occurrence > fromUtc ? occurrence : null;
            if (date == DateOnly.MinValue) return null;
            date = date.AddDays(-1);
        }
    }

    public DateTime? GetNextOccurrence(TimeOnly dailyTime, TimeZoneInfo timeZone, DateTime afterUtc)
    {
        var after = EnsureUtc(afterUtc);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(after, timeZone));
        while (true)
        {
            var occurrence = GetOccurrence(date, dailyTime, timeZone);
            if (occurrence > after) return occurrence;
            if (date == DateOnly.MaxValue) return null;
            date = date.AddDays(1);
        }
    }

    private static DateTime GetOccurrence(DateOnly date, TimeOnly dailyTime, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(dailyTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local)) local = FirstValidLocalTime(local, timeZone);

        if (timeZone.IsAmbiguousTime(local))
        {
            // The larger offset maps to the earlier instant. A repeated wall-clock
            // time represents one daily occurrence, even during the second pass.
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private static DateTime FirstValidLocalTime(DateTime local, TimeZoneInfo timeZone)
    {
        var invalidTicks = local.Ticks;
        var valid = local;
        do
        {
            valid = valid.AddMinutes(1);
        } while (timeZone.IsInvalidTime(valid));

        // Find the end of the gap exactly, including when the scheduled time has
        // seconds or fractional seconds. Do not assume transitions last one hour.
        var validTicks = valid.Ticks;
        while (validTicks - invalidTicks > 1)
        {
            var middle = invalidTicks + (validTicks - invalidTicks) / 2;
            if (timeZone.IsInvalidTime(new DateTime(middle, DateTimeKind.Unspecified))) invalidTicks = middle;
            else validTicks = middle;
        }
        return new DateTime(validTicks, DateTimeKind.Unspecified);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
