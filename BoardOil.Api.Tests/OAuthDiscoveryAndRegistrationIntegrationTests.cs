using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Mcp;
using BoardOil.Contracts.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Tests;

public sealed class OAuthDiscoveryAndRegistrationIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    [Fact]
    public async Task ActiveProjectConnection_ShouldAdvertiseProtectedResourceAndAuthorizationMetadata()
    {
        // Arrange
        var client = Factory.CreateClient();
        var connection = await CreateProjectConnectionAsync(
            client,
            [MachinePatScopes.McpRead]);

        // Act
        var resourceResponse = await client.GetAsync(connection.ResourceUrl);
        var metadataResponse = await client.GetAsync(
            $"/.well-known/oauth-protected-resource/mcp/connections/{connection.PublicId}");
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<OAuthProtectedResourceMetadata>();
        var discovery = await client.GetFromJsonAsync<JsonElement>(
            "https://localhost/.well-known/oauth-authorization-server");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, resourceResponse.StatusCode);
        Assert.Contains(
            $"resource_metadata=\"https://boardoil.example.com/.well-known/oauth-protected-resource/mcp/connections/{connection.PublicId}\"",
            resourceResponse.Headers.WwwAuthenticate.ToString());
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        Assert.NotNull(metadata);
        Assert.Equal($"https://boardoil.example.com{connection.ResourceUrl}", metadata!.Resource);
        Assert.Equal(["https://boardoil.example.com/"], metadata.AuthorizationServers);
        Assert.Equal([MachinePatScopes.McpRead], metadata.ScopesSupported);
        Assert.Equal(["header"], metadata.BearerMethodsSupported);

        Assert.Equal("https://boardoil.example.com/", discovery.GetProperty("issuer").GetString());
        Assert.Equal(
            "https://boardoil.example.com/connect/authorize",
            discovery.GetProperty("authorization_endpoint").GetString());
        Assert.Equal(
            "https://boardoil.example.com/connect/token",
            discovery.GetProperty("token_endpoint").GetString());
        Assert.Equal(
            "https://boardoil.example.com/connect/register",
            discovery.GetProperty("registration_endpoint").GetString());
        Assert.Equal(
            "https://boardoil.example.com/.well-known/jwks",
            discovery.GetProperty("jwks_uri").GetString());
        Assert.Equal(
            ["mcp:read", "mcp:write"],
            discovery.GetProperty("scopes_supported").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(
            ["S256"],
            discovery.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(
            ["none"],
            discovery.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task UnknownOrRevokedProjectConnection_ShouldNotAdvertiseAuthorizableResource()
    {
        // Arrange
        var client = Factory.CreateClient();
        var connection = await CreateProjectConnectionAsync(client, [MachinePatScopes.McpRead]);
        var revokeResponse = await client.DeleteAsync($"/api/system/mcp-project-connections/{connection.Id}");
        revokeResponse.EnsureSuccessStatusCode();

        // Act
        var revokedResponse = await client.GetAsync(
            $"/.well-known/oauth-protected-resource/mcp/connections/{connection.PublicId}");
        var unknownResponse = await client.GetAsync(
            $"/.well-known/oauth-protected-resource/mcp/connections/{new string('a', 64)}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, revokedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task DynamicRegistration_WhenCodexMetadataValid_ShouldCreatePublicPkceClientOnly()
    {
        // Arrange
        var client = Factory.CreateClient();
        await CreateProjectConnectionAsync(
            client,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]);
        var request = CreateCodexRegistrationRequest();

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.StartsWith("bo_oauth_", registration!.ClientId);
        Assert.Equal(request.RedirectUris, registration.RedirectUris);
        Assert.Equal("none", registration.TokenEndpointAuthMethod);
        Assert.Equal("mcp:read mcp:write", registration.Scope);
        Assert.Equal("native", registration.ApplicationType);

        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(registration.ClientId);
        Assert.NotNull(application);
        Assert.Equal(ClientTypes.Public, await manager.GetClientTypeAsync(application!));
        Assert.Contains(
            Requirements.Features.ProofKeyForCodeExchange,
            await manager.GetRequirementsAsync(application!));
        Assert.Contains(
            Permissions.Prefixes.Scope + MachinePatScopes.McpWrite,
            await manager.GetPermissionsAsync(application!));

        var userCount = await ArrangeAsyncForCount();
        Assert.Equal(2, userCount);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenPkceMissing_ShouldBeRejectedBeforeLogin()
    {
        // Arrange
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var connection = await CreateProjectConnectionAsync(client, [MachinePatScopes.McpRead]);
        var registrationResponse = await client.PostAsJsonAsync(
            "/connect/register",
            CreateCodexRegistrationRequest() with { Scope = MachinePatScopes.McpRead });
        var registration = await registrationResponse.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();
        Assert.NotNull(registration);
        var redirectUri = registration!.RedirectUris.Single();
        var authorizationUrl =
            "https://localhost/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(registration.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(MachinePatScopes.McpRead)}"
            + $"&resource={Uri.EscapeDataString($"https://boardoil.example.com{connection.ResourceUrl}")}";

        // Act
        var response = await client.GetAsync(authorizationUrl);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("error:invalid_request", error);
        Assert.Contains("code_challenge", error);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenPkceMethodPlain_ShouldBeRejectedBeforeLogin()
    {
        // Arrange
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var connection = await CreateProjectConnectionAsync(client, [MachinePatScopes.McpRead]);
        var registrationResponse = await client.PostAsJsonAsync(
            "/connect/register",
            CreateCodexRegistrationRequest() with { Scope = MachinePatScopes.McpRead });
        var registration = await registrationResponse.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();
        Assert.NotNull(registration);
        var redirectUri = registration!.RedirectUris.Single();
        var authorizationUrl =
            "https://localhost/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(registration.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(MachinePatScopes.McpRead)}"
            + $"&resource={Uri.EscapeDataString($"https://boardoil.example.com{connection.ResourceUrl}")}"
            + "&code_challenge=insecure-plain-challenge"
            + "&code_challenge_method=plain";

        // Act
        var response = await client.GetAsync(authorizationUrl);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("error:invalid_request", error);
        Assert.Contains("code_challenge_method", error);
    }

    public static TheoryData<OAuthDynamicClientRegistrationRequest, string> InvalidRegistrations =>
        new()
        {
            {
                CreateCodexRegistrationRequest() with
                {
                    RedirectUris = ["https://attacker.example.com/callback"]
                },
                "invalid_redirect_uri"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    RedirectUris = ["http://localhost:49152/callback/project"]
                },
                "invalid_redirect_uri"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    GrantTypes = [GrantTypes.ClientCredentials]
                },
                "invalid_client_metadata"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    ResponseTypes = [ResponseTypes.Token]
                },
                "invalid_client_metadata"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    TokenEndpointAuthMethod = ClientAuthenticationMethods.ClientSecretBasic
                },
                "invalid_client_metadata"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    Scope = "api:system"
                },
                "invalid_client_metadata"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    AdditionalMetadata = new Dictionary<string, JsonElement>
                    {
                        ["jwks_uri"] = JsonSerializer.SerializeToElement("https://attacker.example.com/jwks")
                    }
                },
                "invalid_client_metadata"
            }
        };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task DynamicRegistration_WhenMetadataUnsupported_ShouldReturnProtocolError(
        OAuthDynamicClientRegistrationRequest request,
        string expectedError)
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var error = await response.Content.ReadFromJsonAsync<OAuthProtocolError>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal(expectedError, error!.Error);
    }

    [Fact]
    public async Task CleanupExpiredRegistrations_ShouldDeleteOnlyExpiredDynamicClients()
    {
        // Arrange
        _ = Factory.CreateClient();
        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var expired = CreateApplicationDescriptor("expired-client", DateTimeOffset.UtcNow.AddMinutes(-1));
        var active = CreateApplicationDescriptor("active-client", DateTimeOffset.UtcNow.AddDays(1));
        await manager.CreateAsync(expired);
        await manager.CreateAsync(active);
        var cleanup = scope.ServiceProvider.GetRequiredService<IOAuthDynamicClientRegistrationService>();

        // Act
        var deleted = await cleanup.CleanupExpiredRegistrationsAsync();

        // Assert
        Assert.Equal(1, deleted);
        Assert.Null(await manager.FindByClientIdAsync("expired-client"));
        Assert.NotNull(await manager.FindByClientIdAsync("active-client"));
    }

    private async Task<McpProjectConnectionDto> CreateProjectConnectionAsync(
        HttpClient client,
        string[] allowedScopes)
    {
        await RegisterInitialAdminAsync(client);
        var configurationResponse = await client.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest("https://boardoil.example.com"));
        configurationResponse.EnsureSuccessStatusCode();
        var clientAccountResponse = await client.PostAsJsonAsync(
            "/api/system/client-accounts",
            new BoardOil.Contracts.Users.CreateClientAccountRequest(
                "repository-client",
                "Repository Client",
                "repository-client@localhost",
                "Standard"));
        clientAccountResponse.EnsureSuccessStatusCode();
        var clientAccount = await clientAccountResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedClientAccountDto>>();
        Assert.NotNull(clientAccount?.Data);

        var connectionResponse = await client.PostAsJsonAsync(
            "/api/system/mcp-project-connections",
            new CreateMcpProjectConnectionRequest(
                clientAccount!.Data!.Account.Id,
                "Repository connection",
                allowedScopes));
        connectionResponse.EnsureSuccessStatusCode();
        var connection = await connectionResponse.Content.ReadFromJsonAsync<ApiEnvelope<McpProjectConnectionDto>>();
        Assert.NotNull(connection?.Data);
        return connection!.Data!;
    }

    private static OAuthDynamicClientRegistrationRequest CreateCodexRegistrationRequest() =>
        new()
        {
            ClientName = "Codex",
            RedirectUris = ["http://127.0.0.1:49152/callback/project"],
            GrantTypes = [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken],
            ResponseTypes = [ResponseTypes.Code],
            TokenEndpointAuthMethod = ClientAuthenticationMethods.None,
            Scope = "mcp:read mcp:write",
        };

    private static OpenIddictApplicationDescriptor CreateApplicationDescriptor(
        string clientId,
        DateTimeOffset expiresAt)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            DisplayName = clientId,
        };
        descriptor.Properties["boardoil:dynamic_registration"] = JsonSerializer.SerializeToElement(true);
        descriptor.Properties["boardoil:registration_expires_at"] = JsonSerializer.SerializeToElement(expiresAt);
        return descriptor;
    }

    private async Task<int> ArrangeAsyncForCount()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOil.Ef.BoardOilDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(dbContext.Users);
    }
}
