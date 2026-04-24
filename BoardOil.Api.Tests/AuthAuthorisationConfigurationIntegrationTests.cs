using System.Net;
using System.Net.Http.Json;
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
    }

    [Fact]
    public async Task StandardUser_UpdateConfiguration_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUser_UpdateConfiguration_WithValidMcpPublicBaseUrl_ShouldPersist()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var putResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com/"));
        var putEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();
        var getResponse = await adminClient.GetAsync("/api/system/configuration");
        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.NotNull(putEnvelope);
        Assert.NotNull(putEnvelope!.Data);
        Assert.Equal("https://boardoil.example.com", putEnvelope.Data!.McpPublicBaseUrl);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getEnvelope);
        Assert.NotNull(getEnvelope!.Data);
        Assert.Equal("https://boardoil.example.com", getEnvelope.Data!.McpPublicBaseUrl);
    }

    [Fact]
    public async Task AdminUser_UpdateConfiguration_WithNullMcpPublicBaseUrl_ShouldClearOverride()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var seedResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));
        seedResponse.EnsureSuccessStatusCode();

        // Act
        var clearResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest(null));
        var clearEnvelope = await clearResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.NotNull(clearEnvelope);
        Assert.NotNull(clearEnvelope!.Data);
        Assert.Null(clearEnvelope.Data!.McpPublicBaseUrl);
    }

    [Theory]
    [InlineData("relative/path")]
    public async Task AdminUser_UpdateConfiguration_WithInvalidMcpPublicBaseUrl_ShouldReturnBadRequest(string invalidValue)
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest(invalidValue));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
    }
}
