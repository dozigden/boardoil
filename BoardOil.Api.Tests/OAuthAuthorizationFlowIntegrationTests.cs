using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Mcp;
using BoardOil.Contracts.Users;
using BoardOil.Ef;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Tests;

public sealed class OAuthAuthorizationFlowIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    private readonly ManualTimeProvider timeProvider = new(DateTimeOffset.UtcNow);

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(timeProvider);
            });

    [Fact]
    public async Task AuthorizationRequest_ShouldUseHumanLoginAndShowExactConsentContext()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        logoutResponse.EnsureSuccessStatusCode();

        // Act
        var anonymousResponse = await client.GetAsync(request.Url);
        var anonymousPage = await anonymousResponse.Content.ReadAsStringAsync();
        await LoginAsAsync(client, "admin", "Password1234!");
        var consentResponse = await client.GetAsync(request.Url);
        var consentPage = await consentResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.Contains("Sign in to BoardOil", anonymousPage);
        Assert.Contains("/api/auth/login", anonymousPage);
        Assert.Equal(HttpStatusCode.OK, consentResponse.StatusCode);
        Assert.Contains("Authorise Codex", consentPage);
        Assert.Contains("Repository Client (repository-client)", consentPage);
        Assert.Contains("Repository connection", consentPage);
        Assert.Contains(WebUtility.HtmlEncode(scenario.Resource), consentPage);
        Assert.Contains(MachinePatScopes.McpRead, consentPage);
        Assert.Contains("Your own board access is not delegated", consentPage);
    }

    [Fact]
    public async Task AuthorizationPages_WhenPublicBaseHasPath_ShouldUsePublicEndpointUrls()
    {
        // Arrange
        const string publicBaseUrl = "https://boardoil.example.com/boardoil";
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            client,
            [MachinePatScopes.McpRead],
            publicBaseUrl: publicBaseUrl);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        logoutResponse.EnsureSuccessStatusCode();

        // Act
        var anonymousResponse = await client.GetAsync(request.Url);
        var anonymousPage = await anonymousResponse.Content.ReadAsStringAsync();
        await LoginAsAsync(client, "admin", "Password1234!");
        var consentResponse = await client.GetAsync(request.Url);
        var consentPage = await consentResponse.Content.ReadAsStringAsync();

        // Assert
        anonymousResponse.EnsureSuccessStatusCode();
        Assert.Contains($"{publicBaseUrl}/api/auth/login", anonymousPage);
        consentResponse.EnsureSuccessStatusCode();
        Assert.Contains(
            $"action=\"{publicBaseUrl}/connect/authorize\"",
            consentPage);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenHumanIsNotAdmin_ShouldReturnAccessDenied()
    {
        // Arrange
        var adminClient = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(adminClient, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "ordinary-user", "Password1234!", "Standard");
        var standardClient = CreateOAuthClient();
        await LoginAsAsync(standardClient, "ordinary-user", "Password1234!");
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var response = await standardClient.GetAsync(request.Url);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=access_denied", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenScopeExceedsConnection_ShouldReturnInvalidScope()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(
            scenario,
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}");

        // Act
        var response = await client.GetAsync(request.Url);

        // Assert
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected an OAuth redirect but received {(int)response.StatusCode}: {responseBody}");
        Assert.Contains("error=invalid_scope", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenScopeExceedsClientRegistration_ShouldRejectRequest()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            client,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite],
            MachinePatScopes.McpRead);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpWrite);

        // Act
        var response = await client.GetAsync(request.Url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains(Errors.InvalidRequest, responseBody);
        Assert.Contains("not allowed to use the specified scope", responseBody);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenResourceDoesNotMatchConnection_ShouldReturnInvalidTarget()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var differentResource = scenario.Resource[..^64] + new string('0', 64);
        var request = CreateAuthorizationRequest(
            scenario,
            MachinePatScopes.McpRead,
            differentResource);

        // Act
        var response = await client.GetAsync(request.Url);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=invalid_target", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenAdministratorDenies_ShouldReturnAccessDenied()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var response = await SubmitDecisionAsync(client, request, "deny");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=access_denied", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task RefreshTokens_ShouldRotateAndRejectReuse()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            client,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(
            scenario,
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}");
        var code = await ApproveAsync(client, scenario, request);

        // Act
        var firstExchange = await ExchangeCodeAsync(client, scenario, request, code);
        var firstRefresh = await RefreshAsync(client, scenario, firstExchange.RefreshToken!);
        var refreshReplay = await RefreshAsync(client, scenario, firstExchange.RefreshToken!);
        var secondRefresh = await RefreshAsync(client, scenario, firstRefresh.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstExchange.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(firstExchange.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(firstExchange.RefreshToken));
        Assert.Equal("mcp:read mcp:write", firstExchange.Scope);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(firstRefresh.RefreshToken));
        Assert.NotEqual(firstExchange.RefreshToken, firstRefresh.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, refreshReplay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, refreshReplay.Error);
        Assert.Equal(HttpStatusCode.BadRequest, secondRefresh.StatusCode);
        Assert.Equal(Errors.InvalidGrant, secondRefresh.Error);

        await using var scope = Factory.Services.CreateAsyncScope();
        var authorizationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorizations = new List<object>();
        await foreach (var authorization in authorizationManager.ListAsync())
        {
            authorizations.Add(authorization);
        }

        var storedAuthorization = Assert.Single(authorizations);
        var properties = await authorizationManager.GetPropertiesAsync(storedAuthorization);
        Assert.Equal(
            scenario.Connection.Id,
            properties["boardoil:project_connection_id"].GetInt32());
        Assert.Equal(
            scenario.ClientAccountId,
            properties["boardoil:client_account_id"].GetInt32());
        Assert.Equal(
            scenario.Resource,
            properties["boardoil:resource"].GetString());
    }

    [Fact]
    public async Task AuthorizationCode_WhenReused_ShouldRejectReplayAndRevokeGrant()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        // Act
        var replay = await ExchangeCodeAsync(client, scenario, request, code);
        var refreshAfterReplay = await RefreshAsync(client, scenario, exchange.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, replay.Error);
        Assert.Equal(HttpStatusCode.BadRequest, refreshAfterReplay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, refreshAfterReplay.Error);
    }

    [Fact]
    public async Task AuthorizationCode_WhenPkceVerifierIsWrong_ShouldReturnInvalidGrant()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var requestWithWrongVerifier = request with { CodeVerifier = CreateCodeVerifier() };

        // Act
        var response = await ExchangeCodeAsync(
            client,
            scenario,
            requestWithWrongVerifier,
            code);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, response.Error);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("client")]
    [InlineData("authorization")]
    public async Task RefreshToken_WhenBindingIsDisabled_ShouldReturnInvalidGrant(string disabledBinding)
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        await DisableBindingAsync(client, scenario, disabledBinding);

        // Act
        var response = await RefreshAsync(client, scenario, exchange.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, response.Error);
    }

    [Fact]
    public async Task AuthorizationCode_WhenExpired_ShouldReturnInvalidGrant()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Act
        var response = await ExchangeCodeAsync(client, scenario, request, code);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, response.Error);
    }

    [Fact]
    public async Task RefreshToken_WhenPublicBaseChanges_ShouldReturnInvalidGrant()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var configurationResponse = await client.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest("https://moved-boardoil.example.com"));
        configurationResponse.EnsureSuccessStatusCode();

        // Act
        var response = await RefreshAsync(client, scenario, exchange.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, response.Error);
    }

    [Fact]
    public async Task McpConnection_WithValidReadToken_ShouldAllowReadsAndRejectWrites()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var endpoint = scenario.Connection.ResourceUrl;

        // Act
        var readResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "board.list", arguments = new { } },
            "oauth-board-list",
            exchange.AccessToken,
            endpoint);
        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(
            readResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OAuth MCP read access but received {(int)readResponse.StatusCode}: {readBody}");
        var writeResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = 1,
                    title = "Must not be created",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "oauth-card-create",
            exchange.AccessToken,
            endpoint);
        var writeBody = await writeResponse.Content.ReadAsStringAsync();
        Assert.True(
            writeResponse.StatusCode == HttpStatusCode.Forbidden,
            $"Expected an insufficient-scope response but received {(int)writeResponse.StatusCode}: {writeBody}");
        var writeChallenge = writeResponse.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_scope\"", writeChallenge);
        Assert.Contains($"scope=\"{MachinePatScopes.McpWrite}\"", writeChallenge);
        Assert.Contains("resource_metadata=", writeChallenge);
        using var readPayload = await McpJsonRpcClient.ParseJsonAsync(readResponse);

        // Assert
        Assert.False(readPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task McpConnection_WhenTokenTargetsDifferentConnection_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var secondConnectionResponse = await client.PostAsJsonAsync(
            "/api/system/mcp-project-connections",
            new CreateMcpProjectConnectionRequest(
                scenario.ClientAccountId,
                "Different repository connection",
                [MachinePatScopes.McpRead]));
        secondConnectionResponse.EnsureSuccessStatusCode();
        var secondConnection = await secondConnectionResponse.Content
            .ReadFromJsonAsync<ApiEnvelope<McpProjectConnectionDto>>();
        Assert.NotNull(secondConnection?.Data);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-cross-connection",
            exchange.AccessToken,
            secondConnection!.Data!.ResourceUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"invalid_token\"", challenge);
        Assert.Contains(
            $"/.well-known/oauth-protected-resource/mcp/connections/{secondConnection.Data.PublicId}",
            challenge);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("client")]
    [InlineData("authorization")]
    public async Task McpConnection_WhenBindingIsDisabled_ShouldRejectExistingAccessToken(
        string disabledBinding)
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        await DisableBindingAsync(client, scenario, disabledBinding);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            $"oauth-disabled-{disabledBinding}",
            exchange.AccessToken,
            scenario.Connection.ResourceUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            "error=\"invalid_token\"",
            response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task McpConnection_WhenMembershipChanges_ShouldUseLiveAccessAndClientActor()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            client,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]);
        var request = CreateAuthorizationRequest(
            scenario,
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}");
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var columnId = await SeedBoardColumnAsync("OAuth actor column");
        var cardId = await SeedBoardCardAsync(columnId, "OAuth actor card", "");
        var beforeMembership = await GetOAuthBoardIdsAsync(client, scenario, exchange.AccessToken!);
        Assert.DoesNotContain(1, beforeMembership);
        var addMembershipResponse = await client.PostAsJsonAsync(
            "/api/system/boards/1/members",
            new AddBoardMemberRequest(scenario.ClientAccountId, "Contributor"));
        addMembershipResponse.EnsureSuccessStatusCode();

        // Act
        var afterMembership = await GetOAuthBoardIdsAsync(client, scenario, exchange.AccessToken!);
        var commentResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.comment.create",
                arguments = new
                {
                    boardId = 1,
                    id = cardId,
                    text = "Created through OAuth"
                }
            },
            "oauth-comment-create",
            exchange.AccessToken,
            scenario.Connection.ResourceUrl);
        commentResponse.EnsureSuccessStatusCode();
        using var commentPayload = await McpJsonRpcClient.ParseJsonAsync(commentResponse);
        var removeMembershipResponse = await client.DeleteAsync(
            $"/api/system/boards/1/members/{scenario.ClientAccountId}");
        removeMembershipResponse.EnsureSuccessStatusCode();
        var afterRemoval = await GetOAuthBoardIdsAsync(client, scenario, exchange.AccessToken!);

        // Assert
        Assert.Contains(1, afterMembership);
        var comment = McpJsonRpcClient.GetStructuredContent(commentPayload).GetProperty("comment");
        Assert.Equal(scenario.ClientAccountId, comment.GetProperty("authorUserId").GetInt32());
        Assert.DoesNotContain(1, afterRemoval);
    }

    [Fact]
    public async Task McpConnection_WithoutToken_ShouldReturnBearerChallenge()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-missing-token",
            endpoint: scenario.Connection.ResourceUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        Assert.Contains(
            $"resource_metadata=\"https://boardoil.example.com/.well-known/oauth-protected-resource/mcp/connections/{scenario.Connection.PublicId}\"",
            response.Headers.WwwAuthenticate.ToString());
        Assert.DoesNotContain("error=", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task McpConnection_WithInvalidBearerToken_ShouldReturnInvalidTokenChallenge()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-invalid-token",
            "not-a-valid-access-token",
            scenario.Connection.ResourceUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"invalid_token\"", challenge);
        Assert.Contains("resource_metadata=", challenge);
    }

    [Fact]
    public async Task McpConnection_WithNonStringProtocolFields_ShouldReturnBadRequest()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        // Act
        var invalidMethodResponse = await SendRawMcpRequestAsync(
            client,
            scenario.Connection.ResourceUrl,
            exchange.AccessToken!,
            """{"jsonrpc":"2.0","id":"invalid-method","method":42}""");
        var invalidNameResponse = await SendRawMcpRequestAsync(
            client,
            scenario.Connection.ResourceUrl,
            exchange.AccessToken!,
            """{"jsonrpc":"2.0","id":"invalid-name","method":"tools/call","params":{"name":42,"arguments":{}}}""");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, invalidMethodResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidNameResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendRawMcpRequestAsync(
        HttpClient client,
        string endpoint,
        string accessToken,
        string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await client.SendAsync(request);
    }

    private static async Task<int[]> GetOAuthBoardIdsAsync(
        HttpClient client,
        OAuthScenario scenario,
        string accessToken)
    {
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "board.list", arguments = new { } },
            $"oauth-board-list-{Guid.NewGuid():N}",
            accessToken,
            scenario.Connection.ResourceUrl);
        response.EnsureSuccessStatusCode();
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);
        return McpJsonRpcClient.GetStructuredContent(payload)
            .GetProperty("boards")
            .EnumerateArray()
            .Select(board => board.GetProperty("id").GetInt32())
            .ToArray();
    }

    private HttpClient CreateOAuthClient()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.BaseAddress = new Uri("https://localhost");
        return client;
    }

    private async Task<OAuthScenario> CreateScenarioAsync(
        HttpClient client,
        string[] allowedScopes,
        string registeredScopes = "mcp:read mcp:write",
        string publicBaseUrl = "https://boardoil.example.com")
    {
        await RegisterInitialAdminAsync(client);
        var configurationResponse = await client.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest(publicBaseUrl));
        configurationResponse.EnsureSuccessStatusCode();
        var clientAccountResponse = await client.PostAsJsonAsync(
            "/api/system/client-accounts",
            new CreateClientAccountRequest(
                "repository-client",
                "Repository Client",
                "repository-client@localhost",
                "Standard"));
        clientAccountResponse.EnsureSuccessStatusCode();
        var clientAccount = await clientAccountResponse.Content
            .ReadFromJsonAsync<ApiEnvelope<CreatedClientAccountDto>>();
        Assert.NotNull(clientAccount?.Data);

        var connectionResponse = await client.PostAsJsonAsync(
            "/api/system/mcp-project-connections",
            new CreateMcpProjectConnectionRequest(
                clientAccount!.Data!.Account.Id,
                "Repository connection",
                allowedScopes));
        connectionResponse.EnsureSuccessStatusCode();
        var connection = await connectionResponse.Content
            .ReadFromJsonAsync<ApiEnvelope<McpProjectConnectionDto>>();
        Assert.NotNull(connection?.Data);

        var redirectUri = "http://127.0.0.1:49152/callback/project";
        var registrationResponse = await client.PostAsJsonAsync(
            "/connect/register",
            new OAuthDynamicClientRegistrationRequest
            {
                ClientName = "Codex",
                RedirectUris = [redirectUri],
                GrantTypes = [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken],
                ResponseTypes = [ResponseTypes.Code],
                TokenEndpointAuthMethod = ClientAuthenticationMethods.None,
                Scope = registeredScopes,
            });
        registrationResponse.EnsureSuccessStatusCode();
        var registration = await registrationResponse.Content
            .ReadFromJsonAsync<OAuthDynamicClientRegistrationResponse>();
        Assert.NotNull(registration);

        return new OAuthScenario(
            connection!.Data!,
            clientAccount.Data.Account.Id,
            registration!.ClientId,
            redirectUri,
            $"{publicBaseUrl}{connection.Data.ResourceUrl}",
            publicBaseUrl);
    }

    private static AuthorizationRequest CreateAuthorizationRequest(
        OAuthScenario scenario,
        string scopes,
        string? resource = null)
    {
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = scenario.OAuthClientId,
            ["redirect_uri"] = scenario.RedirectUri,
            ["response_type"] = ResponseTypes.Code,
            ["scope"] = scopes,
            ["resource"] = resource ?? scenario.Resource,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = CodeChallengeMethods.Sha256,
            ["state"] = state,
        };
        var query = string.Join(
            '&',
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new AuthorizationRequest($"/connect/authorize?{query}", parameters, verifier);
    }

    private static async Task<string> ApproveAsync(
        HttpClient client,
        OAuthScenario scenario,
        AuthorizationRequest request)
    {
        var approvalResponse = await SubmitDecisionAsync(client, request, "approve");
        Assert.Equal(HttpStatusCode.Redirect, approvalResponse.StatusCode);
        var location = approvalResponse.Headers.Location
            ?? throw new InvalidOperationException("The OAuth approval did not return a redirect.");
        var code = ParseQuery(location.Query, "code");
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal($"{scenario.PublicBaseUrl}/", ParseQuery(location.Query, "iss"));
        return code;
    }

    private static async Task<HttpResponseMessage> SubmitDecisionAsync(
        HttpClient client,
        AuthorizationRequest request,
        string decision)
    {
        var consentResponse = await client.GetAsync(request.Url);
        consentResponse.EnsureSuccessStatusCode();
        var consentPage = await consentResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(consentPage);
        var form = new Dictionary<string, string>(request.Parameters, StringComparer.Ordinal)
        {
            ["boardoil_oauth_antiforgery"] = antiforgeryToken,
            ["decision"] = decision,
        };

        return await client.PostAsync(
            "/connect/authorize",
            new FormUrlEncodedContent(form));
    }

    private static async Task<TokenResponse> ExchangeCodeAsync(
        HttpClient client,
        OAuthScenario scenario,
        AuthorizationRequest request,
        string code)
    {
        var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.AuthorizationCode,
                ["client_id"] = scenario.OAuthClientId,
                ["code"] = code,
                ["redirect_uri"] = scenario.RedirectUri,
                ["code_verifier"] = request.CodeVerifier,
            }));
        return await ReadTokenResponseAsync(response);
    }

    private static async Task<TokenResponse> RefreshAsync(
        HttpClient client,
        OAuthScenario scenario,
        string refreshToken)
    {
        var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.RefreshToken,
                ["client_id"] = scenario.OAuthClientId,
                ["refresh_token"] = refreshToken,
            }));
        return await ReadTokenResponseAsync(response);
    }

    private async Task DisableBindingAsync(
        HttpClient client,
        OAuthScenario scenario,
        string disabledBinding)
    {
        if (disabledBinding == "connection")
        {
            var response = await client.DeleteAsync(
                $"/api/system/mcp-project-connections/{scenario.Connection.Id}");
            response.EnsureSuccessStatusCode();
            return;
        }

        if (disabledBinding == "client")
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
            await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
            var account = await dbContext.Users.SingleAsync(x => x.Id == scenario.ClientAccountId);
            account.IsActive = false;
            await dbContext.SaveChangesAsync();
            return;
        }

        if (disabledBinding == "authorization")
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            object? authorization = null;
            await foreach (var candidate in manager.ListAsync())
            {
                authorization = candidate;
                break;
            }

            Assert.NotNull(authorization);
            Assert.True(await manager.TryRevokeAsync(authorization!));
            return;
        }

        throw new InvalidOperationException($"Unknown disabled binding '{disabledBinding}'.");
    }

    private static async Task<TokenResponse> ReadTokenResponseAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TokenResponse(
            response.StatusCode,
            GetOptionalString(payload, "access_token"),
            GetOptionalString(payload, "refresh_token"),
            GetOptionalString(payload, "scope"),
            GetOptionalString(payload, "error"));
    }

    private static string? GetOptionalString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var property) ? property.GetString() : null;

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"boardoil_oauth_antiforgery\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The consent page did not contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string ParseQuery(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2
                && string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return string.Empty;
    }

    private static string CreateCodeVerifier() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    private static string CreateCodeChallenge(string verifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record OAuthScenario(
        McpProjectConnectionDto Connection,
        int ClientAccountId,
        string OAuthClientId,
        string RedirectUri,
        string Resource,
        string PublicBaseUrl);

    private sealed record AuthorizationRequest(
        string Url,
        Dictionary<string, string> Parameters,
        string CodeVerifier);

    private sealed record TokenResponse(
        HttpStatusCode StatusCode,
        string? AccessToken,
        string? RefreshToken,
        string? Scope,
        string? Error);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtc = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtc;

        public void Advance(TimeSpan duration) => currentUtc = currentUtc.Add(duration);
    }
}
