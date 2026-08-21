using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Abstractions.OAuth;
using BoardOil.Api.OAuth;

namespace BoardOil.Api.Configuration;

public sealed class ConfigurationService(
    JwtAuthOptions jwtOptions,
    OAuthTokenAuditCaptureState oauthTokenAuditCaptureState,
    IDbContextScopeFactory scopeFactory,
    IAppSettingRepository appSettingRepository) : IConfigurationService
{
    private const string McpPublicBaseUrlKey = "mcp_public_base_url";

    public async Task<ApiResult<ConfigurationDto>> GetConfigurationAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();
        var mcpPublicBaseUrl = await GetSettingValueAsync(McpPublicBaseUrlKey);
        return CreateConfigurationDto(mcpPublicBaseUrl);
    }

    public async Task<ApiResult<ConfigurationDto>> UpdateConfigurationAsync(UpdateConfigurationRequest request)
    {
        var normalisedBaseUrlResult = NormaliseMcpPublicBaseUrl(request.McpPublicBaseUrl);
        if (!normalisedBaseUrlResult.Success)
        {
            return normalisedBaseUrlResult.Error!;
        }

        using var scope = scopeFactory.Create();
        var existingSetting = await appSettingRepository.GetByKeyAsync(McpPublicBaseUrlKey);
        var existingOAuthDiagnosticsSetting = await appSettingRepository.GetByKeyAsync(
            OAuthTokenAuditCaptureState.SettingKey);
        var normalisedBaseUrl = normalisedBaseUrlResult.Value;
        var hasChanges = false;
        if (normalisedBaseUrl is null)
        {
            if (existingSetting is not null)
            {
                appSettingRepository.Remove(existingSetting);
                hasChanges = true;
            }
        }
        else
        {
            if (existingSetting is null)
            {
                appSettingRepository.Add(new EntityAppSetting
                {
                    Key = McpPublicBaseUrlKey,
                    Value = normalisedBaseUrl
                });
                hasChanges = true;
            }
            else if (!string.Equals(existingSetting.Value, normalisedBaseUrl, StringComparison.Ordinal))
            {
                existingSetting.Value = normalisedBaseUrl;
                hasChanges = true;
            }
        }

        var diagnosticsValue = request.OAuthLifecycleDiagnosticsEnabled
            ? "true"
            : "false";
        if (existingOAuthDiagnosticsSetting is null)
        {
            appSettingRepository.Add(new EntityAppSetting
            {
                Key = OAuthTokenAuditCaptureState.SettingKey,
                Value = diagnosticsValue
            });
            hasChanges = true;
        }
        else if (!string.Equals(
            existingOAuthDiagnosticsSetting.Value,
            diagnosticsValue,
            StringComparison.OrdinalIgnoreCase))
        {
            existingOAuthDiagnosticsSetting.Value = diagnosticsValue;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await scope.SaveChangesAsync();
        }

        oauthTokenAuditCaptureState.SetEnabled(request.OAuthLifecycleDiagnosticsEnabled);
        return CreateConfigurationDto(normalisedBaseUrl);
    }

    public async Task<string?> GetMcpPublicBaseUrlAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();
        return await GetSettingValueAsync(McpPublicBaseUrlKey);
    }

    private async Task<string?> GetSettingValueAsync(string key)
    {
        var setting = await appSettingRepository.GetByKeyAsync(key);
        return string.IsNullOrWhiteSpace(setting?.Value)
            ? null
            : setting.Value.Trim();
    }

    private ConfigurationDto CreateConfigurationDto(string? mcpPublicBaseUrl) =>
        new(
            jwtOptions.AllowInsecureCookies,
            mcpPublicBaseUrl,
            oauthTokenAuditCaptureState.IsEnabled,
            OAuthTokenAuditRetention.RetentionDays);

    private static (bool Success, string? Value, ApiError? Error) NormaliseMcpPublicBaseUrl(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (true, null, null);
        }

        var trimmed = rawValue.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return (false, null, ApiErrors.BadRequest("mcpPublicBaseUrl must be an absolute URL."));
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return (false, null, ApiErrors.BadRequest("mcpPublicBaseUrl must use http or https."));
        }

        if (!string.IsNullOrWhiteSpace(uri.Query) || !string.IsNullOrWhiteSpace(uri.Fragment))
        {
            return (false, null, ApiErrors.BadRequest("mcpPublicBaseUrl cannot include query string or fragment."));
        }

        var normalised = trimmed.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalised)
            ? (false, null, ApiErrors.BadRequest("mcpPublicBaseUrl is invalid."))
            : (true, normalised, null);
    }
}
