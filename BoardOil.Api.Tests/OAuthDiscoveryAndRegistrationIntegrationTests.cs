using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Tests;

public sealed class OAuthDiscoveryAndRegistrationIntegrationTests
    : AuthAuthorisationIntegrationTestBase, IClassFixture<OAuthDiscoveryAndRegistrationFixture>
{
    public OAuthDiscoveryAndRegistrationIntegrationTests(OAuthDiscoveryAndRegistrationFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Fact]
    public async Task SharedMcpResource_ShouldAdvertiseProtectedResourceAndAuthorizationMetadata()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);

        // Act
        var resourceResponse = await client.GetAsync("/mcp");
        var metadataResponse = await client.GetAsync(
            "/.well-known/oauth-protected-resource/mcp");
        var legacyMetadataResponse = await client.GetAsync(
            "/.well-known/oauth-protected-resource/mcp/oauth");
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<OAuthProtectedResourceMetadata>();
        var legacyMetadata = await legacyMetadataResponse.Content
            .ReadFromJsonAsync<OAuthProtectedResourceMetadata>();
        var discovery = await client.GetFromJsonAsync<JsonElement>(
            "https://localhost/.well-known/oauth-authorization-server");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, resourceResponse.StatusCode);
        Assert.Contains(
            "resource_metadata=\"https://boardoil.example.com/.well-known/oauth-protected-resource/mcp\"",
            resourceResponse.Headers.WwwAuthenticate.ToString());
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        Assert.NotNull(metadata);
        Assert.Equal("https://boardoil.example.com/mcp", metadata!.Resource);
        Assert.Equal(["https://boardoil.example.com/"], metadata.AuthorizationServers);
        Assert.Equal([MachinePatScopes.McpRead, MachinePatScopes.McpWrite], metadata.ScopesSupported);
        Assert.Equal(["header"], metadata.BearerMethodsSupported);
        Assert.Equal(HttpStatusCode.OK, legacyMetadataResponse.StatusCode);
        Assert.Equal("https://boardoil.example.com/mcp/oauth", legacyMetadata!.Resource);

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
    public async Task SharedMcpMetadata_ShouldNotRequireAPreCreatedConnection()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);

        // Act
        var response = await client.GetAsync(
            "/.well-known/oauth-protected-resource/mcp");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DynamicRegistration_WhenCodexMetadataValid_ShouldCreatePublicPkceClientOnly()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
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
        Assert.True(await manager.HasApplicationTypeAsync(application!, ApplicationTypes.Native));
        Assert.Contains(
            Requirements.Features.ProofKeyForCodeExchange,
            await manager.GetRequirementsAsync(application!));
        Assert.Contains(
            Permissions.Prefixes.Scope + MachinePatScopes.McpWrite,
            await manager.GetPermissionsAsync(application!));

        var userCount = await ArrangeAsyncForCount();
        Assert.Equal(1, userCount);
    }

    [Fact]
    public async Task DynamicRegistration_WhenScopeOmitted_ShouldRegisterDefaultMcpScopes()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
        var request = JsonSerializer.SerializeToNode(
            CreateCodexRegistrationRequest() with { ClientName = "GitHub Copilot CLI" })!.AsObject();
        request.Remove("scope");

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal("mcp:read mcp:write", registration!.Scope);

        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(registration.ClientId);
        Assert.NotNull(application);
        var permissions = await manager.GetPermissionsAsync(application!);
        Assert.Contains(Permissions.Prefixes.Scope + MachinePatScopes.McpRead, permissions);
        Assert.Contains(Permissions.Prefixes.Scope + MachinePatScopes.McpWrite, permissions);
    }

    [Fact]
    public async Task DynamicRegistration_WhenVsCodeMetadataValid_ShouldRegisterNativeCallbacks()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
        var request = CreateCodexRegistrationRequest() with
        {
            ClientName = "Visual Studio Code",
            ClientUri = "https://code.visualstudio.com",
            ApplicationType = ApplicationTypes.Native,
            RedirectUris =
            [
                "https://insiders.vscode.dev/redirect",
                "https://vscode.dev/redirect",
                "http://127.0.0.1/",
                "http://127.0.0.1:33418/"
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal(request.ClientUri, registration!.ClientUri);
        Assert.Equal(request.RedirectUris, registration.RedirectUris);
        Assert.Equal(ApplicationTypes.Native, registration.ApplicationType);

        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(registration.ClientId);
        Assert.NotNull(application);
        Assert.True(await manager.HasApplicationTypeAsync(application!, ApplicationTypes.Native));
    }

    [Fact]
    public async Task DynamicRegistration_WhenPublicWebClientValid_ShouldPersistWebApplicationType()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
        var request = CreateCodexRegistrationRequest() with
        {
            ClientName = "Browser MCP client",
            ApplicationType = ApplicationTypes.Web,
            RedirectUris = ["https://client.example.com/oauth/callback"]
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal(ApplicationTypes.Web, registration!.ApplicationType);

        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(registration.ClientId);
        Assert.NotNull(application);
        Assert.True(await manager.HasApplicationTypeAsync(application!, ApplicationTypes.Web));
    }

    [Fact]
    public async Task DynamicRegistration_WhenOptionalFlowMetadataOmitted_ShouldReturnDefaultsWithoutNullMetadata()
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
        var request = JsonSerializer.SerializeToNode(CreateCodexRegistrationRequest())!.AsObject();
        request.Remove("grant_types");
        request.Remove("response_types");
        request.Remove("client_uri");

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [GrantTypes.AuthorizationCode],
            responseJson.GetProperty("grant_types").EnumerateArray().Select(static value => value.GetString()));
        Assert.Equal(
            [ResponseTypes.Code],
            responseJson.GetProperty("response_types").EnumerateArray().Select(static value => value.GetString()));
        Assert.False(responseJson.TryGetProperty("client_uri", out _));
    }

    [Fact]
    public async Task DynamicRegistration_WhenUnknownOptionalMetadataSupplied_ShouldIgnoreIt()
    {
        // Arrange
        var client = CreateClient();
        var request = CreateCodexRegistrationRequest() with
        {
            AdditionalMetadata = new Dictionary<string, JsonElement>
            {
                ["software_id"] = JsonSerializer.SerializeToElement("visual-studio-code")
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DynamicRegistration_WhenRedirectUriRepeated_ShouldRegisterItOnce()
    {
        // Arrange
        var client = CreateClient();
        var request = CreateCodexRegistrationRequest() with
        {
            RedirectUris =
            [
                "http://127.0.0.1:49152/callback/project",
                "http://127.0.0.1:49152/callback/project"
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal(["http://127.0.0.1:49152/callback/project"], registration!.RedirectUris);
    }

    [Fact]
    public async Task DynamicRegistration_WhenEquivalentRedirectUrisRepeated_ShouldReturnPersistedRedirectUriOnce()
    {
        // Arrange
        var client = CreateClient();
        var request = CreateCodexRegistrationRequest() with
        {
            ApplicationType = ApplicationTypes.Web,
            RedirectUris =
            [
                "https://CLIENT.example.com/oauth/callback",
                "https://client.example.com/oauth/callback"
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal([request.RedirectUris[0]], registration!.RedirectUris);

        await using var scope = Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(registration.ClientId);
        Assert.NotNull(application);
        Assert.Equal(registration.RedirectUris, await manager.GetRedirectUrisAsync(application!));
    }

    [Fact]
    public async Task AuthorizationRequest_WhenNativeClientUsesEphemeralLoopbackPort_ShouldReachLogin()
    {
        // Arrange
        var client = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        }));
        await ConfigurePublicBaseAsync(client);
        var registrationResponse = await client.PostAsJsonAsync(
            "/connect/register",
            CreateCodexRegistrationRequest() with
            {
                ApplicationType = ApplicationTypes.Native,
                RedirectUris = ["http://127.0.0.1/"]
            });
        var registration = await registrationResponse.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();
        Assert.NotNull(registration);
        const string redirectUri = "http://127.0.0.1:43821/";
        var authorizationUrl =
            "https://localhost/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(registration!.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(MachinePatScopes.McpRead)}"
            + $"&resource={Uri.EscapeDataString("https://boardoil.example.com/mcp")}"
            + $"&code_challenge={new string('A', 43)}"
            + "&code_challenge_method=S256";

        // Act
        var response = await client.GetAsync(authorizationUrl);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("error:", responseBody);
    }

    [Theory]
    [InlineData("http://localhost:49152/callback")]
    [InlineData("http://localhost/callback")]
    [InlineData("https://client.example.com/oauth/callback")]
    public async Task DynamicRegistration_WhenSafeRedirectUriSupplied_ShouldAcceptRedirectUri(
        string redirectUri)
    {
        // Arrange
        var client = CreateClient();
        await ConfigurePublicBaseAsync(client);
        var request = CreateCodexRegistrationRequest() with
        {
            ClientName = "Claude Code",
            RedirectUris = [redirectUri]
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal(request.RedirectUris, registration!.RedirectUris);
    }

    [Fact]
    public async Task DynamicRegistration_WhenClientUriUsesHttpLoopback_ShouldAcceptIt()
    {
        // Arrange
        var client = CreateClient();
        var request = CreateCodexRegistrationRequest() with
        {
            ClientUri = "http://localhost:3000/about"
        };

        // Act
        var response = await client.PostAsJsonAsync("/connect/register", request);
        var registration = await response.Content.ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal(request.ClientUri, registration!.ClientUri);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenPkceMissing_ShouldBeRejectedBeforeLogin()
    {
        // Arrange
        var client = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        }));
        await ConfigurePublicBaseAsync(client);
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
            + $"&resource={Uri.EscapeDataString("https://boardoil.example.com/mcp/oauth")}";

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
        var client = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        }));
        await ConfigurePublicBaseAsync(client);
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
            + $"&resource={Uri.EscapeDataString("https://boardoil.example.com/mcp/oauth")}"
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
                    ClientUri = "http://code.visualstudio.com"
                },
                "invalid_client_metadata"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    RedirectUris = ["http://attacker.example.com:49152/callback"]
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
                    Scope = "   "
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
                    RedirectUris = ["com.example.client:/oauth/callback"]
                },
                "invalid_redirect_uri"
            },
            {
                CreateCodexRegistrationRequest() with
                {
                    ApplicationType = ApplicationTypes.Web,
                    RedirectUris = ["http://127.0.0.1:49152/callback"]
                },
                "invalid_redirect_uri"
            }
        };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task DynamicRegistration_WhenMetadataUnsupported_ShouldReturnProtocolError(
        OAuthDynamicClientRegistrationRequest request,
        string expectedError)
    {
        // Arrange
        var client = CreateClient();

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
        _ = CreateClient();
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

    private async Task ConfigurePublicBaseAsync(HttpClient client)
    {
        await RegisterInitialAdminAsync(client);
        var configurationResponse = await client.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest("https://boardoil.example.com"));
        configurationResponse.EnsureSuccessStatusCode();
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

public sealed class OAuthDiscoveryAndRegistrationFixture : IAsyncLifetime, IResettableApiFactoryFixture
{
    public OAuthDiscoveryAndRegistrationFixture()
    {
        DatabasePath = ApiFactoryIntegrationTestBase.BuildDbPath(
            nameof(OAuthDiscoveryAndRegistrationIntegrationTests));
        Factory = new BoardOilApiFactory(
            DatabasePath,
            configureTestServices: services =>
                OAuthTestServiceConfiguration.DisableDynamicClientRegistrationRateLimit(
                    services,
                    "oauth-discovery-registration-tests"));
    }

    public string DatabasePath { get; }
    public BoardOilApiFactory Factory { get; }

    public ValueTask InitializeAsync()
    {
        using var client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync()
    {
        Factory.ResetDatabaseFromTemplate();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
    }
}
