using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using BoardOil.Services.Jobs;

namespace BoardOil.Services.Configuration;

public sealed class SystemTimeZoneService(
    IDbContextScopeFactory scopes,
    IAppSettingRepository settings,
    IScheduledJobSchedulerStateRepository states,
    SchedulingGate schedulingGate,
    TimeProvider clock) : ISystemTimeZoneService
{
    public ApiError? Validate(string? timeZoneId) => SystemTimeZoneSettings.Validate(timeZoneId);

    public ApiResult<SystemTimeZoneOptionsDto> GetOptions()
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var options = SystemTimeZoneSettings.GetSupportedIds().Select(id =>
        {
            var zone = SystemTimeZoneSettings.ResolveOrUtc(id);
            var offset = zone.GetUtcOffset(nowUtc);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var absolute = offset.Duration();
            return new SystemTimeZoneOptionDto(
                id, $"{id.Replace('_', ' ')} [UTC{sign}{(int)absolute.TotalHours:00}:{absolute.Minutes:00}]");
        }).ToArray();
        return ApiResults.Ok(new SystemTimeZoneOptionsDto(SystemTimeZoneSettings.DefaultId, options));
    }

    public async Task<TimeZoneInfo> GetConfiguredTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        var setting = await settings.GetByKeyAsync(SystemTimeZoneSettings.Key);
        cancellationToken.ThrowIfCancellationRequested();
        return SystemTimeZoneSettings.ResolveOrUtc(setting?.Value);
    }

    public async Task<ApiResult<SystemTimeZoneDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        var setting = await settings.GetByKeyAsync(SystemTimeZoneSettings.Key);
        cancellationToken.ThrowIfCancellationRequested();
        var id = SystemTimeZoneSettings.Validate(setting?.Value) is null
            ? setting!.Value.Trim()
            : SystemTimeZoneSettings.DefaultId;
        return ApiResults.Ok(new SystemTimeZoneDto(id));
    }

    public async Task<ApiResult<SystemTimeZoneDto>> UpdateAsync(
        UpdateSystemTimeZoneRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = Validate(request.SystemTimeZoneId);
        if (error is not null)
        {
            return error with { ValidationErrors = new() { ["systemTimeZoneId"] = [error.Message] } };
        }

        using var coordination = await schedulingGate.EnterAsync(cancellationToken);
        var id = request.SystemTimeZoneId.Trim();
        using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
        await ApplyValidatedChangeAsync(id, cancellationToken);
        await scope.SaveChangesAsync(cancellationToken);
        return ApiResults.Ok(new SystemTimeZoneDto(id));
    }

    public async Task<bool> ApplyValidatedChangeAsync(string timeZoneId, CancellationToken cancellationToken = default)
    {
        var id = timeZoneId.Trim();
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var setting = await settings.GetByKeyAsync(SystemTimeZoneSettings.Key);
        var changed = setting is null || !string.Equals(setting.Value, id, StringComparison.Ordinal);
        var previousId = SystemTimeZoneSettings.Validate(setting?.Value) is null
            ? setting!.Value.Trim()
            : SystemTimeZoneSettings.DefaultId;
        var timingChanged = !string.Equals(previousId, id, StringComparison.Ordinal);
        if (setting is null)
        {
            settings.Add(new EntityAppSetting { Key = SystemTimeZoneSettings.Key, Value = id });
        }
        else if (changed)
        {
            setting.Value = id;
        }

        if (timingChanged)
        {
            foreach (var state in await states.ListAsync(cancellationToken))
            {
                state.LastRunTimeUtc = nowUtc;
            }
        }

        return changed;
    }
}
