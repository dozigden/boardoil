using BoardOil.Contracts.Common;

namespace BoardOil.Services.Configuration;

public static class SystemTimeZoneSettings
{
    public const string Key = "system_timezone";
    public const string DefaultId = "UTC";

    public static ApiError? Validate(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return ApiErrors.BadRequest("System timezone is required.");
        }

        var id = timeZoneId.Trim();
        if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))
        {
            return ApiErrors.BadRequest(
                "System timezone must be a valid IANA timezone identifier available on this system.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return null;
        }
        catch (TimeZoneNotFoundException)
        {
            return ApiErrors.BadRequest(
                "System timezone must be a valid IANA timezone identifier available on this system.");
        }
        catch (InvalidTimeZoneException)
        {
            return ApiErrors.BadRequest(
                "System timezone must be a valid IANA timezone identifier available on this system.");
        }
    }

    public static TimeZoneInfo ResolveOrUtc(string? timeZoneId) =>
        Validate(timeZoneId) is null
            ? TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!.Trim())
            : TimeZoneInfo.Utc;

    public static IReadOnlyList<string> GetSupportedIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { DefaultId };
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            var id = zone.Id;
            if (Validate(id) is not null)
            {
                if (!TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
                {
                    continue;
                }

                id = ianaId;
            }

            if (Validate(id) is null)
            {
                ids.Add(id);
            }
        }

        return ids.Order(StringComparer.Ordinal).ToArray();
    }
}
