using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Configuration;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Api.Configuration;

public sealed class ConfigurationService(
    JwtAuthOptions jwtOptions,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory,
    IAppSettingRepository appSettingRepository) : IConfigurationService
{
    private const string McpPublicBaseUrlKey = "mcp_public_base_url";

    public async Task<ApiResult<ConfigurationDto>> GetConfigurationAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();
        var mcpPublicBaseUrl = await GetSettingValueAsync(McpPublicBaseUrlKey);
        return new ConfigurationDto(jwtOptions.AllowInsecureCookies, mcpPublicBaseUrl);
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var normalisedBaseUrl = normalisedBaseUrlResult.Value;
        if (normalisedBaseUrl is null)
        {
            if (existingSetting is not null)
            {
                appSettingRepository.Remove(existingSetting);
                await scope.SaveChangesAsync();
            }
        }
        else
        {
            if (existingSetting is null)
            {
                appSettingRepository.Add(new EntityAppSetting
                {
                    Key = McpPublicBaseUrlKey,
                    Value = normalisedBaseUrl,
                    UpdatedAtUtc = now
                });
                await scope.SaveChangesAsync();
            }
            else if (!string.Equals(existingSetting.Value, normalisedBaseUrl, StringComparison.Ordinal))
            {
                existingSetting.Value = normalisedBaseUrl;
                existingSetting.UpdatedAtUtc = now;
                await scope.SaveChangesAsync();
            }
        }

        return new ConfigurationDto(jwtOptions.AllowInsecureCookies, normalisedBaseUrl);
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
