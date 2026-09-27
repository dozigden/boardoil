using BoardOil.Contracts.Common;

namespace BoardOil.Services.Configuration;

public static class SystemTimeZoneSettings
{
    public const string Key = "system_timezone";
    public const string DefaultId = "UTC";
    private static readonly Lazy<IReadOnlyList<string>> SupportedIds = new(CreateSupportedIds);

    public static ApiError? Validate(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return ApiErrors.BadRequest("System timezone is required.");
        }

        return GetSupportedIds().Contains(timeZoneId.Trim(), StringComparer.Ordinal)
            ? null
            : ApiErrors.BadRequest("Choose a timezone from the supported timezone list.");
    }

    public static TimeZoneInfo ResolveOrUtc(string? timeZoneId) =>
        Validate(timeZoneId) is null
            ? TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!.Trim())
            : TimeZoneInfo.Utc;

    public static IReadOnlyList<string> GetSupportedIds() => SupportedIds.Value;

    private static IReadOnlyList<string> CreateSupportedIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { DefaultId };
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            var id = zone.Id;
            if (!CanResolveIanaId(id))
            {
                if (!TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
                {
                    continue;
                }

                id = ianaId;
            }

            if (CanResolveIanaId(id))
            {
                ids.Add(id);
            }
        }

        return Array.AsReadOnly(ids.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool CanResolveIanaId(string id)
    {
        if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
