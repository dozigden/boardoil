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

}
