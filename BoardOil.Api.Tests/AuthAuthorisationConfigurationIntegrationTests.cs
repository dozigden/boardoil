using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.OAuth;
using BoardOil.Api.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthAuthorisationConfigurationIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    [Fact]
    public async Task AdminUser_GetConfiguration_ShouldReturnOk()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.GetAsync("/api/system/configuration");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.True(envelope.Data!.AllowInsecureCookies);
        Assert.Null(envelope.Data.McpPublicBaseUrl);
        Assert.False(envelope.Data.OAuthLifecycleDiagnosticsEnabled);
        Assert.Equal(
            OAuthTokenAuditRetention.RetentionDays,
            envelope.Data.OAuthLifecycleDiagnosticsRetentionDays);
    }

    [Fact]
    public async Task AdminUser_UpdateConfiguration_ShouldApplyOAuthDiagnosticsImmediately()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest(null, true));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(envelope!.Data!.OAuthLifecycleDiagnosticsEnabled);
        var captureState = Factory.Services
            .GetRequiredService<OAuthTokenAuditCaptureState>();
        Assert.True(captureState.IsEnabled);
    }

    [Fact]
    public async Task AuthenticatedUser_GetSystemInfoMessage_ShouldReturnOk()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.GetAsync("/api/system/system-info-message");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SystemInfoMessageEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.Null(envelope!.Data);
    }

    private sealed record SystemInfoMessageEnvelope(
        bool Enabled,
        string? Emoji,
        string Title,
        string Description,
        string StyleName,
        string StylePropertiesJson);
}
