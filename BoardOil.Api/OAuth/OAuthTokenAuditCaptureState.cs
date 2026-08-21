using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Configuration;

namespace BoardOil.Api.OAuth;

public sealed class OAuthTokenAuditCaptureState
{
    internal const string SettingKey = "oauth_lifecycle_diagnostics_enabled";

    private int enabled;

    public bool IsEnabled => Volatile.Read(ref enabled) == 1;

    public void SetEnabled(bool isEnabled) =>
        Volatile.Write(ref enabled, isEnabled ? 1 : 0);
}

public static class OAuthTokenAuditCaptureStartupExtensions
{
    public static async Task InitializeOAuthTokenAuditCaptureStateAsync(
        this IServiceProvider serviceProvider)
    {
        using var serviceScope = serviceProvider.CreateScope();
        var scopeFactory = serviceScope.ServiceProvider
            .GetRequiredService<IDbContextScopeFactory>();
        var repository = serviceScope.ServiceProvider
            .GetRequiredService<IAppSettingRepository>();
        var captureState = serviceProvider
            .GetRequiredService<OAuthTokenAuditCaptureState>();

        using var scope = scopeFactory.CreateReadOnly();
        var setting = await repository.GetByKeyAsync(OAuthTokenAuditCaptureState.SettingKey);
        var isEnabled = bool.TryParse(setting?.Value?.Trim(), out var parsedValue)
            && parsedValue;
        captureState.SetEnabled(isEnabled);
    }
}
