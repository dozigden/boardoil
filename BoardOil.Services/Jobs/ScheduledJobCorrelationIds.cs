using System.Globalization;

namespace BoardOil.Services.Jobs;

public static class ScheduledJobCorrelationIds
{
    public static string Create(string scheduleName, DateTime dueAtUtc, string? targetKey = null)
    {
        var target = targetKey is null ? string.Empty : $":{targetKey}";
        var utc = dueAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc)
            : dueAtUtc.ToUniversalTime();
        return $"scheduled:{scheduleName}{target}:{utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}";
    }
}
