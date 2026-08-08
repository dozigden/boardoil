using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Tests;

public sealed class OAuthDynamicClientRegistrationRateLimitIntegrationTests
    : ApiFactoryIntegrationTestBase
{
    [Fact]
    public async Task DynamicRegistration_WhenPermitLimitExceeded_ShouldReturnTooManyRequests()
    {
        // Arrange
        var client = CreateClient();
        var permitLimit = Factory.Services
            .GetRequiredService<BoardOilOAuthOptions>()
            .DynamicClientRegistrationLimitPerMinute;
        var request = new OAuthDynamicClientRegistrationRequest
        {
            ClientName = "Codex",
            RedirectUris = ["http://127.0.0.1:49152/callback/project"],
            GrantTypes = [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken],
            ResponseTypes = [ResponseTypes.Code],
            TokenEndpointAuthMethod = ClientAuthenticationMethods.None,
            Scope = "mcp:read mcp:write",
        };

        for (var requestIndex = 0; requestIndex < permitLimit; requestIndex++)
        {
            using var acceptedResponse = await client.PostAsJsonAsync("/connect/register", request);
            Assert.Equal(HttpStatusCode.Created, acceptedResponse.StatusCode);
        }

        // Act
        using var response = await client.PostAsJsonAsync("/connect/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
