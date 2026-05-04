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

}
