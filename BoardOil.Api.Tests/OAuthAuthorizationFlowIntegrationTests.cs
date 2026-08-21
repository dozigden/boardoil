using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardOil.Abstractions.OAuth;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;
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

public sealed class OAuthAuthorizationFlowIntegrationTests : AuthAuthorisationIntegrationTestBase, IClassFixture<OAuthAuthorizationFlowFixture>
{
    private const string RegistrationExpiresAtProperty = "boardoil:registration_expires_at";
    private readonly ManualTimeProvider timeProvider;

    public OAuthAuthorizationFlowIntegrationTests(OAuthAuthorizationFlowFixture fixture)
    {
        UseSharedFactory(fixture);
        timeProvider = fixture.TimeProvider;
    }

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
        Assert.Contains("(@admin)", consentPage);
        Assert.Contains("Sign in as another user", consentPage);
        Assert.Contains("Switching account also changes the BoardOil user signed into this browser.", consentPage);
        Assert.Contains("/api/auth/login", consentPage);
        Assert.Contains($"name=\"consenting_user_id\" value=\"{scenario.UserId}\"", consentPage);
        Assert.Contains("Connection name", consentPage);
        Assert.Contains("Redirect URI", consentPage);
        Assert.Contains(WebUtility.HtmlEncode(scenario.Resource), consentPage);
        Assert.Contains(MachinePatScopes.McpRead, consentPage);
        Assert.Contains("your signed-in BoardOil user", consentPage);
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
        Assert.Contains($"fetch(\"{publicBaseUrl}/api/auth/login\"", consentPage);
    }

    [Fact]
    public async Task AccountSwitch_WhenCredentialsAreRejected_ShouldKeepCurrentConsentUser()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", "incorrect-password"));
        var consentResponse = await client.GetAsync(request.Url);
        var consentPage = await consentResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        consentResponse.EnsureSuccessStatusCode();
        Assert.Contains("admin (@admin)", consentPage);
        Assert.Contains($"name=\"consenting_user_id\" value=\"{scenario.UserId}\"", consentPage);
    }

    [Fact]
    public async Task AccountSwitch_WhenCredentialsAreAccepted_ShouldCreateConnectionForNewUser()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var switchedUserId = await CreateUserAsAdminAsync(
            client,
            "oauth-switch-user",
            "Password1234!",
            "Standard");
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        await LoginAsAsync(client, "oauth-switch-user", "Password1234!");
        var consentResponse = await client.GetAsync(request.Url);
        var consentPage = await consentResponse.Content.ReadAsStringAsync();
        var switchedScenario = scenario with { UserId = switchedUserId };
        var code = await ApproveAsync(client, switchedScenario, request);
        var exchange = await ExchangeCodeAsync(client, switchedScenario, request, code);

        // Assert
        Assert.Contains("oauth-switch-user (@oauth-switch-user)", consentPage);
        Assert.Contains($"name=\"consenting_user_id\" value=\"{switchedUserId}\"", consentPage);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var connection = await dbContext.OAuthConnections.SingleAsync();
        Assert.Equal(switchedUserId, connection.UserId);
    }

    [Fact]
    public async Task Approval_WhenSessionUserChangedAfterConsentRendered_ShouldRequireFreshConsent()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        _ = await CreateUserAsAdminAsync(
            client,
            "oauth-stale-user",
            "Password1234!",
            "Standard");
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var consentResponse = await client.GetAsync(request.Url);
        consentResponse.EnsureSuccessStatusCode();
        var consentPage = await consentResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(consentPage);
        await LoginAsAsync(client, "oauth-stale-user", "Password1234!");

        // Act
        var response = await PostDecisionAsync(
            client,
            scenario,
            request,
            "approve",
            antiforgeryToken,
            scenario.UserId);
        var responsePage = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("signed-in BoardOil user changed while consent was open", responsePage);
        Assert.Contains("oauth-stale-user (@oauth-stale-user)", responsePage);
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        Assert.Empty(await dbContext.OAuthConnections.ToArrayAsync());
    }

    [Fact]
    public async Task AuthorizationRequest_WhenUserIsNotAdmin_ShouldAllowSelfConsent()
    {
        // Arrange
        var adminClient = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(adminClient, [MachinePatScopes.McpRead]);
        var adminRequest = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var adminCode = await ApproveAsync(adminClient, scenario, adminRequest);
        var adminExchange = await ExchangeCodeAsync(
            adminClient,
            scenario,
            adminRequest,
            adminCode);
        Assert.Equal(HttpStatusCode.OK, adminExchange.StatusCode);
        var ordinaryUserId = await CreateUserAsAdminAsync(
            adminClient,
            "ordinary-user",
            "Password1234!",
            "Standard");
        var standardClient = CreateOAuthClient();
        await LoginAsAsync(standardClient, "ordinary-user", "Password1234!");
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var response = await standardClient.GetAsync(request.Url);
        var responsePage = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ordinary-user (@ordinary-user)", responsePage);
        Assert.DoesNotContain("client_account_id", responsePage);
        Assert.Contains("const existingConnections=[]", responsePage);

        var standardScenario = scenario with { UserId = ordinaryUserId };
        var code = await ApproveAsync(standardClient, standardScenario, request);
        var exchange = await ExchangeCodeAsync(standardClient, standardScenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var connections = await dbContext.OAuthConnections
            .OrderBy(x => x.Id)
            .ToArrayAsync();
        Assert.Equal(2, connections.Length);
        Assert.Equal(scenario.UserId, connections[0].UserId);
        Assert.Equal(ordinaryUserId, connections[1].UserId);
        Assert.Equal(connections[0].Name, connections[1].Name);
    }

    [Fact]
    public async Task Approval_WhenScopeWasNotRequested_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var response = await SubmitDecisionAsync(
            client,
            scenario,
            request,
            "approve",
            [MachinePatScopes.McpWrite]);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "Approved scopes must be a subset of the requested MCP scopes.",
            await response.Content.ReadAsStringAsync());
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
        var differentResource = $"{scenario.PublicBaseUrl}/mcp/other";
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
    public async Task AuthorizationRequest_WhenResourceSchemeAndHostCaseDiffer_ShouldAcceptResource()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(
            scenario,
            MachinePatScopes.McpRead,
            "HTTPS://BOARDOIL.EXAMPLE.COM/mcp/oauth");

        // Act
        var response = await client.GetAsync(request.Url);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Authorise Codex", responseBody);
    }

    [Fact]
    public async Task AuthorizationRequest_WhenUserDenies_ShouldReturnAccessDenied()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var response = await SubmitDecisionAsync(client, scenario, request, "deny");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=access_denied", response.Headers.Location?.Query);

        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        Assert.Empty(await dbContext.OAuthConnections.ToArrayAsync());
        Assert.Empty(await dbContext.OAuthConnectionGrants.ToArrayAsync());

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applicationManager.FindByClientIdAsync(scenario.OAuthClientId);
        Assert.NotNull(application);
        var properties = await applicationManager.GetPropertiesAsync(application!);
        Assert.Contains(RegistrationExpiresAtProperty, properties);
    }

    [Fact]
    public async Task Approval_WhenSubsetOfRequestedScopesIsSelected_ShouldIssueOnlySelectedScope()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(
            scenario,
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}");

        // Act
        var approval = await SubmitDecisionAsync(
            client,
            scenario,
            request,
            "approve",
            [MachinePatScopes.McpRead]);
        Assert.Equal(HttpStatusCode.Redirect, approval.StatusCode);
        var code = ParseQuery(approval.Headers.Location!.Query, "code");
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);

        // Assert
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        Assert.Equal(MachinePatScopes.McpRead, exchange.Scope);

        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var grant = await dbContext.OAuthConnectionGrants.SingleAsync();
        Assert.Equal(MachinePatScopes.McpRead, grant.ApprovedScopesCsv);
    }

    [Fact]
    public async Task RefreshToken_WhenRetriedWithinLeeway_ShouldReturnUsableReplacement()
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
        var refreshRetry = await RefreshAsync(client, scenario, firstExchange.RefreshToken!);
        var refreshAfterRetry = await RefreshAsync(client, scenario, refreshRetry.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstExchange.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(firstExchange.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(firstExchange.RefreshToken));
        Assert.Equal("mcp:read mcp:write", firstExchange.Scope);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(firstRefresh.RefreshToken));
        Assert.NotEqual(firstExchange.RefreshToken, firstRefresh.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshRetry.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshRetry.RefreshToken));
        Assert.NotEqual(firstExchange.RefreshToken, refreshRetry.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshAfterRetry.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshAfterRetry.RefreshToken));

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
            scenario.UserId,
            properties["boardoil:user_id"].GetInt32());
        Assert.Equal(
            scenario.Resource,
            properties["boardoil:resource"].GetString());

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applicationManager.FindByClientIdAsync(scenario.OAuthClientId);
        Assert.NotNull(application);
        var applicationProperties = await applicationManager.GetPropertiesAsync(application!);
        Assert.DoesNotContain(
            RegistrationExpiresAtProperty,
            applicationProperties);
    }

    [Fact]
    public async Task RefreshToken_WhenUsedConcurrently_ShouldReturnUsableReplacements()
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
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);

        // Act
        var concurrentRefreshes = await Task.WhenAll(
            RefreshAsync(client, scenario, exchange.RefreshToken!),
            RefreshAsync(client, scenario, exchange.RefreshToken!));
        var followUpRefreshes = await Task.WhenAll(
            concurrentRefreshes.Select(response =>
                RefreshAsync(client, scenario, response.RefreshToken!)));

        // Assert
        Assert.All(
            concurrentRefreshes,
            response =>
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
            });
        Assert.All(
            followUpRefreshes,
            response =>
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
            });
    }

    [Fact]
    public async Task RefreshToken_WhenRetriedAfterLeeway_ShouldRejectAndRevokeReplacement()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        await RegisterInitialAdminAsync(client);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        var refresh = await RefreshAsync(client, scenario, exchange.RefreshToken!);
        var options = Factory.Services.GetRequiredService<BoardOilOAuthOptions>();
        timeProvider.Advance(options.RefreshTokenReuseLeeway + TimeSpan.FromSeconds(1));

        // Act
        var replay = await RefreshAsync(client, scenario, exchange.RefreshToken!);
        var refreshAfterReplay = await RefreshAsync(client, scenario, refresh.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, replay.Error);
        Assert.Equal(HttpStatusCode.BadRequest, refreshAfterReplay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, refreshAfterReplay.Error);
    }

    [Fact]
    public async Task TokenAudit_ShouldCorrelateIssuanceSuccessfulRefreshAndRejectedReplayWithoutExposingCredentials()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            client,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]);
        await ConfigureOAuthDiagnosticsAsync(client, scenario.PublicBaseUrl, true);
        var request = CreateAuthorizationRequest(
            scenario,
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}");
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        var requestedScopes = $"{MachinePatScopes.McpWrite} {MachinePatScopes.McpRead}";
        var refresh = await RefreshAsync(client, scenario, exchange.RefreshToken!, requestedScopes);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var options = Factory.Services.GetRequiredService<BoardOilOAuthOptions>();
        timeProvider.Advance(options.RefreshTokenReuseLeeway + TimeSpan.FromSeconds(1));

        // Act
        var replay = await RefreshAsync(client, scenario, exchange.RefreshToken!, requestedScopes);
        var auditResponse = await client.GetAsync(
            $"/api/system/oauth-token-audits?clientId={Uri.EscapeDataString(scenario.OAuthClientId)}");
        var auditJson = await auditResponse.Content.ReadAsStringAsync();
        var auditResult = JsonSerializer.Deserialize<ApiResult<OAuthTokenAuditListDto>>(
            auditJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var persistedAudits = await dbContext.OAuthTokenAudits
            .AsNoTracking()
            .Where(audit => audit.OAuthClientId == scenario.OAuthClientId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var persistedAuditJson = JsonSerializer.Serialize(persistedAudits);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(Errors.InvalidGrant, replay.Error);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        Assert.NotNull(auditResult?.Data);
        Assert.Equal(3, auditResult!.Data!.TotalCount);
        var issuance = Assert.Single(
            auditResult.Data.Items,
            audit => audit.GrantType == GrantTypes.AuthorizationCode
                && audit.Outcome == OAuthTokenAuditOutcomes.Succeeded);
        var refreshed = Assert.Single(
            auditResult.Data.Items,
            audit => audit.GrantType == GrantTypes.RefreshToken
                && audit.Outcome == OAuthTokenAuditOutcomes.Succeeded);
        var rejected = Assert.Single(
            auditResult.Data.Items,
            audit => audit.GrantType == GrantTypes.RefreshToken
                && audit.Outcome == OAuthTokenAuditOutcomes.Rejected);
        Assert.False(string.IsNullOrWhiteSpace(issuance.IssuedRefreshTokenFingerprint));
        Assert.Equal(issuance.IssuedRefreshTokenFingerprint, refreshed.PresentedTokenFingerprint);
        Assert.Equal(refreshed.PresentedTokenFingerprint, rejected.PresentedTokenFingerprint);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.IssuedRefreshTokenFingerprint));
        Assert.NotEqual(refreshed.PresentedTokenFingerprint, refreshed.IssuedRefreshTokenFingerprint);
        Assert.Null(issuance.RequestedScopes);
        Assert.Equal(
            $"{MachinePatScopes.McpRead} {MachinePatScopes.McpWrite}",
            refreshed.RequestedScopes);
        Assert.Equal(refreshed.RequestedScopes, rejected.RequestedScopes);
        Assert.Equal(issuance.AuthorizationId, refreshed.AuthorizationId);
        Assert.Equal(refreshed.AuthorizationId, rejected.AuthorizationId);
        Assert.Equal(Errors.InvalidGrant, rejected.ErrorCode);
        Assert.Contains("ID2012", rejected.ErrorUri, StringComparison.Ordinal);
        Assert.Equal("Repository connection", rejected.OAuthConnectionName);
        Assert.Equal("admin", rejected.OwnerUserName);
        Assert.Equal("Codex", rejected.OAuthClientDisplayName);
        Assert.Equal(scenario.Resource, rejected.Resource);
        Assert.DoesNotContain(code, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(exchange.RefreshToken!, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(refresh.RefreshToken!, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(code, persistedAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(exchange.RefreshToken!, persistedAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(refresh.RefreshToken!, persistedAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("presentedTokenId", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("subject", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("createdAtUtc", auditJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenAudit_WhenCaptureIsToggled_ShouldOnlyPersistEventsWhileEnabled()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        await ConfigureOAuthDiagnosticsAsync(client, scenario.PublicBaseUrl, true);

        // Act
        var capturedRefresh = await RefreshAsync(client, scenario, exchange.RefreshToken!);
        await ConfigureOAuthDiagnosticsAsync(client, scenario.PublicBaseUrl, false);
        var ignoredRefresh = await RefreshAsync(client, scenario, capturedRefresh.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        Assert.Equal(HttpStatusCode.OK, capturedRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ignoredRefresh.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var audit = await dbContext.OAuthTokenAudits.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(GrantTypes.RefreshToken, audit.GrantType);
        Assert.False(string.IsNullOrWhiteSpace(audit.IssuedRefreshTokenFingerprint));
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

    [Fact]
    public async Task AuthorizationCode_WhenResourceOmitted_ShouldReturnInvalidTarget()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);

        // Act
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
        var tokenResponse = await ReadTokenResponseAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
        Assert.Equal(Errors.InvalidTarget, tokenResponse.Error);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("user")]
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
        await DisableBindingAsync(scenario, disabledBinding);

        // Act
        var response = await RefreshAsync(client, scenario, exchange.RefreshToken!);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidGrant, response.Error);
    }

    [Fact]
    public async Task RevokeOwnConnection_ShouldInvalidateCurrentAccessAndRefreshTokens()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        int connectionId;
        string authorizationId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
            await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
            var connection = await dbContext.OAuthConnections
                .Include(x => x.ActiveGrant)
                .SingleAsync();
            connectionId = connection.Id;
            authorizationId = connection.ActiveGrant!.OpenIddictAuthorizationId;
        }

        // Act
        var revokeResponse = await client.DeleteAsync($"/api/oauth-connections/{connectionId}");
        var refreshResponse = await RefreshAsync(client, scenario, exchange.RefreshToken!);
        var accessResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "revoked-oauth-connection",
            exchange.AccessToken,
            "/mcp/oauth");

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        Assert.Equal(Errors.InvalidGrant, refreshResponse.Error);
        Assert.Equal(HttpStatusCode.Unauthorized, accessResponse.StatusCode);

        await using var assertScope = Factory.Services.CreateAsyncScope();
        var authorizationManager = assertScope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizationManager.FindByIdAsync(authorizationId);
        Assert.NotNull(authorization);
        Assert.True(await authorizationManager.HasStatusAsync(authorization!, Statuses.Revoked));
        var assertFactory = assertScope.ServiceProvider
            .GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var assertDbContext = assertFactory.CreateDbContext<BoardOilDbContext>();
        Assert.False(await assertDbContext.OAuthConnections.AnyAsync(x => x.Id == connectionId));
        Assert.False(await assertDbContext.OAuthConnectionGrants
            .AnyAsync(x => x.OAuthConnectionId == connectionId));
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
    public async Task CanonicalMcpConnection_WithValidReadToken_ShouldAllowReadsAndRejectWrites()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(
            scenario,
            MachinePatScopes.McpRead,
            $"{scenario.PublicBaseUrl}/mcp");
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        const string endpoint = "/mcp";

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
        var identityResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "identity_get", arguments = new { } },
            "oauth-identity-get",
            exchange.AccessToken,
            endpoint);
        var identityBody = await identityResponse.Content.ReadAsStringAsync();
        Assert.True(
            identityResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OAuth MCP identity access but received {(int)identityResponse.StatusCode}: {identityBody}");
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
        using var identityPayload = await McpJsonRpcClient.ParseJsonAsync(identityResponse);

        // Assert
        Assert.False(readPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var identity = McpJsonRpcClient.GetStructuredContent(identityPayload);
        Assert.Equal("admin", identity.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal("OAuth", identity.GetProperty("authentication").GetProperty("type").GetString());
        Assert.Equal(
            [MachinePatScopes.McpRead],
            identity.GetProperty("authentication").GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()).ToArray());
    }

    [Theory]
    [InlineData("/mcp", "/mcp/oauth", "/.well-known/oauth-protected-resource/mcp/oauth")]
    [InlineData("/mcp/oauth", "/mcp", "/.well-known/oauth-protected-resource/mcp")]
    public async Task McpConnection_WhenTokenAudienceDoesNotMatchEndpoint_ShouldRejectToken(
        string tokenResourcePath,
        string endpoint,
        string expectedMetadataPath)
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(
            scenario,
            MachinePatScopes.McpRead,
            $"{scenario.PublicBaseUrl}{tokenResourcePath}");
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            $"audience-mismatch-{tokenResourcePath}",
            exchange.AccessToken,
            endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"invalid_token\"", challenge);
        Assert.Contains(
            $"resource_metadata=\"{scenario.PublicBaseUrl}{expectedMetadataPath}\"",
            challenge);
    }

    [Fact]
    public async Task McpConnection_WithValidReadToken_ShouldSupportLegacyProtocol()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        const string endpoint = "/mcp/oauth";

        // Act
        var initializeResponse = await McpJsonRpcClient.SendLegacyInitializeAsync(
            client,
            "oauth-legacy-initialize",
            exchange.AccessToken,
            endpoint);
        var toolsResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-legacy-tools",
            exchange.AccessToken,
            endpoint);
        var readResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/call",
            new { name = "board.list", arguments = new { } },
            "oauth-legacy-board-list",
            exchange.AccessToken,
            endpoint);
        var writeResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = 1,
                    title = "Must not be created through legacy OAuth",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "oauth-legacy-card-create",
            exchange.AccessToken,
            endpoint);
        using var toolsPayload = await McpJsonRpcClient.ParseJsonAsync(toolsResponse);
        using var readPayload = await McpJsonRpcClient.ParseJsonAsync(readResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.False(initializeResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
        Assert.NotEmpty(toolsPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray());
        Assert.False(readPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains(
            $"scope=\"{MachinePatScopes.McpWrite}\"",
            writeResponse.Headers.WwwAuthenticate.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task McpConnection_WithValidToken_ShouldRejectUnsupportedGetWithoutInvalidatingToken()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/mcp/oauth");
        getRequest.Headers.Authorization = new("Bearer", exchange.AccessToken);
        getRequest.Headers.Accept.ParseAdd("text/event-stream");
        getRequest.Headers.TryAddWithoutValidation(
            "MCP-Protocol-Version",
            McpJsonRpcClient.ModernProtocolVersion);

        // Act
        var response = await client.SendAsync(getRequest);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(
            HttpMethod.Post.Method,
            Assert.Single(response.Content.Headers.Allow));
        Assert.DoesNotContain(
            "invalid_token",
            response.Headers.WwwAuthenticate.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task McpConnection_WhenValid_ShouldTrackLastUseAtMostOncePerUtcDay()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var request = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var code = await ApproveAsync(client, scenario, request);
        var exchange = await ExchangeCodeAsync(client, scenario, request, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var previousDay = timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        await SetOnlyConnectionLastUsedAtUtcAsync(previousDay);

        // Act
        var firstResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-last-used-first",
            exchange.AccessToken,
            "/mcp/oauth");
        var firstLastUsedAtUtc = await GetOnlyConnectionLastUsedAtUtcAsync();
        var sameDayEarlier = timeProvider.GetUtcNow().UtcDateTime.Date;
        await SetOnlyConnectionLastUsedAtUtcAsync(sameDayEarlier);
        var secondResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-last-used-second",
            exchange.AccessToken,
            "/mcp/oauth");
        var secondLastUsedAtUtc = await GetOnlyConnectionLastUsedAtUtcAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, firstLastUsedAtUtc);
        Assert.Equal(sameDayEarlier, secondLastUsedAtUtc);
    }

    [Fact]
    public async Task Reauthorization_WhenNameIsReused_ShouldRequireWarningAndReplacePreviousGrant()
    {
        // Arrange
        var client = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var firstRequest = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);
        var firstCode = await ApproveAsync(client, scenario, firstRequest);
        var firstExchange = await ExchangeCodeAsync(client, scenario, firstRequest, firstCode);
        Assert.Equal(HttpStatusCode.OK, firstExchange.StatusCode);
        var lastUsedAtUtc = timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        await SetOnlyConnectionLastUsedAtUtcAsync(lastUsedAtUtc);
        var replacementRequest = CreateAuthorizationRequest(scenario, MachinePatScopes.McpRead);

        // Act
        var unconfirmed = await SubmitDecisionAsync(
            client,
            scenario,
            replacementRequest,
            "approve");
        var replacementApproval = await SubmitDecisionAsync(
            client,
            scenario,
            replacementRequest,
            "approve",
            replaceExisting: true);
        var replacementCode = ParseQuery(replacementApproval.Headers.Location!.Query, "code");
        var replacementExchange = await ExchangeCodeAsync(
            client,
            scenario,
            replacementRequest,
            replacementCode);
        var oldTokenResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-replaced-grant",
            firstExchange.AccessToken,
            "/mcp/oauth");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        var unconfirmedPage = await unconfirmed.Content.ReadAsStringAsync();
        Assert.Contains("Confirm replacement", unconfirmedPage);
        Assert.Contains("Repository connection", unconfirmedPage);
        Assert.Equal(HttpStatusCode.Redirect, replacementApproval.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replacementExchange.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var connection = await dbContext.OAuthConnections.SingleAsync();
        var grants = await dbContext.OAuthConnectionGrants.OrderBy(x => x.Id).ToArrayAsync();
        Assert.Equal("Repository connection", connection.Name);
        Assert.Equal(lastUsedAtUtc, connection.LastUsedAtUtc);
        Assert.Equal(2, grants.Length);
        Assert.Equal("replaced", grants[0].RevocationReason);
        Assert.Equal(grants[1].Id, connection.ActiveGrantId);
    }

    [Fact]
    public async Task Reauthorization_WhenNameDiffers_ShouldCreateIndependentConnection()
    {
        // Arrange
        var client = CreateOAuthClient();
        var firstScenario = await CreateScenarioAsync(client, [MachinePatScopes.McpRead]);
        var firstRequest = CreateAuthorizationRequest(firstScenario, MachinePatScopes.McpRead);
        var firstCode = await ApproveAsync(client, firstScenario, firstRequest);
        var firstExchange = await ExchangeCodeAsync(client, firstScenario, firstRequest, firstCode);
        Assert.Equal(HttpStatusCode.OK, firstExchange.StatusCode);
        var secondScenario = firstScenario with { ConnectionName = "Second installation" };
        var secondRequest = CreateAuthorizationRequest(secondScenario, MachinePatScopes.McpRead);

        // Act
        var secondCode = await ApproveAsync(client, secondScenario, secondRequest);
        var secondExchange = await ExchangeCodeAsync(client, secondScenario, secondRequest, secondCode);
        var firstTokenResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-first-independent-connection",
            firstExchange.AccessToken,
            "/mcp/oauth");
        var secondTokenResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "oauth-second-independent-connection",
            secondExchange.AccessToken,
            "/mcp/oauth");

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstTokenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondTokenResponse.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var connections = await dbContext.OAuthConnections.OrderBy(x => x.Id).ToArrayAsync();
        Assert.Equal(2, connections.Length);
        Assert.Equal("Repository connection", connections[0].Name);
        Assert.Equal("Second installation", connections[1].Name);
        Assert.All(connections, connection => Assert.NotNull(connection.ActiveGrantId));
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("user")]
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
        await DisableBindingAsync(scenario, disabledBinding);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            $"oauth-disabled-{disabledBinding}",
            exchange.AccessToken,
            "/mcp/oauth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            "error=\"invalid_token\"",
            response.Headers.WwwAuthenticate.ToString());
        Assert.Null(await GetOnlyConnectionLastUsedAtUtcAsync());
    }

    [Fact]
    public async Task McpConnection_WhenMembershipChanges_ShouldUseLiveAccessAndUserActor()
    {
        // Arrange
        var adminClient = CreateOAuthClient();
        var scenario = await CreateScenarioAsync(
            adminClient,
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]);
        var userId = await CreateUserAsAdminAsync(
            adminClient,
            "oauth-member",
            "Password1234!",
            "Standard");
        scenario = scenario with { UserId = userId };
        var client = CreateOAuthClient();
        await LoginAsAsync(client, "oauth-member", "Password1234!");
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
        var addMembershipResponse = await adminClient.PostAsJsonAsync(
            "/api/system/boards/1/members",
            new AddBoardMemberRequest(scenario.UserId, "Contributor"));
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
            "/mcp/oauth");
        commentResponse.EnsureSuccessStatusCode();
        using var commentPayload = await McpJsonRpcClient.ParseJsonAsync(commentResponse);
        var removeMembershipResponse = await adminClient.DeleteAsync(
            $"/api/system/boards/1/members/{scenario.UserId}");
        removeMembershipResponse.EnsureSuccessStatusCode();
        var afterRemoval = await GetOAuthBoardIdsAsync(client, scenario, exchange.AccessToken!);

        // Assert
        Assert.Contains(1, afterMembership);
        var comment = McpJsonRpcClient.GetStructuredContent(commentPayload).GetProperty("comment");
        Assert.Equal(scenario.UserId, comment.GetProperty("authorUserId").GetInt32());
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
            endpoint: "/mcp/oauth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        Assert.Contains(
            "resource_metadata=\"https://boardoil.example.com/.well-known/oauth-protected-resource/mcp/oauth\"",
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
            "/mcp/oauth");

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
            "/mcp/oauth",
            exchange.AccessToken!,
            """{"jsonrpc":"2.0","id":"invalid-method","method":42}""");
        var invalidNameResponse = await SendRawMcpRequestAsync(
            client,
            "/mcp/oauth",
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

    private async Task<DateTime?> GetOnlyConnectionLastUsedAtUtcAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        return await dbContext.OAuthConnections
            .Select(x => x.LastUsedAtUtc)
            .SingleAsync();
    }

    private async Task SetOnlyConnectionLastUsedAtUtcAsync(DateTime value)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var connection = await dbContext.OAuthConnections.SingleAsync();
        connection.LastUsedAtUtc = value;
        await dbContext.SaveChangesAsync();
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
            "/mcp/oauth");
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
        var client = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        }));
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
        await using var scope = Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
        await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
        var userId = await dbContext.Users
            .Where(x => x.UserName == "admin")
            .Select(x => x.Id)
            .SingleAsync();

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
            userId,
            registration!.ClientId,
            redirectUri,
            $"{publicBaseUrl}/mcp/oauth",
            publicBaseUrl,
            "Repository connection",
            allowedScopes);
    }

    private static async Task ConfigureOAuthDiagnosticsAsync(
        HttpClient client,
        string publicBaseUrl,
        bool enabled)
    {
        var response = await client.PutAsJsonAsync(
            "/api/system/configuration",
            new UpdateConfigurationRequest(publicBaseUrl, enabled));
        response.EnsureSuccessStatusCode();
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
        var approvalResponse = await SubmitDecisionAsync(client, scenario, request, "approve");
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
        OAuthScenario scenario,
        AuthorizationRequest request,
        string decision,
        string[]? approvedScopes = null,
        bool replaceExisting = false)
    {
        var consentResponse = await client.GetAsync(request.Url);
        consentResponse.EnsureSuccessStatusCode();
        var consentPage = await consentResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(consentPage);
        return await PostDecisionAsync(
            client,
            scenario,
            request,
            decision,
            antiforgeryToken,
            scenario.UserId,
            approvedScopes,
            replaceExisting);
    }

    private static async Task<HttpResponseMessage> PostDecisionAsync(
        HttpClient client,
        OAuthScenario scenario,
        AuthorizationRequest request,
        string decision,
        string antiforgeryToken,
        int consentingUserId,
        string[]? approvedScopes = null,
        bool replaceExisting = false)
    {
        var form = request.Parameters
            .Select(static pair => new KeyValuePair<string, string>(pair.Key, pair.Value))
            .ToList();
        form.Add(new("boardoil_oauth_antiforgery", antiforgeryToken));
        form.Add(new("consenting_user_id", consentingUserId.ToString()));
        form.Add(new("decision", decision));
        form.Add(new("connection_name", scenario.ConnectionName));
        if (replaceExisting)
        {
            form.Add(new("replace_existing", "true"));
        }

        foreach (var scope in approvedScopes ?? scenario.ApprovalScopes)
        {
            form.Add(new("approved_scope", scope));
        }

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
                ["resource"] = request.Parameters["resource"],
            }));
        return await ReadTokenResponseAsync(response);
    }

    private static async Task<TokenResponse> RefreshAsync(
        HttpClient client,
        OAuthScenario scenario,
        string refreshToken,
        string? scope = null)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.RefreshToken,
            ["client_id"] = scenario.OAuthClientId,
            ["refresh_token"] = refreshToken,
            ["resource"] = scenario.Resource,
        };
        if (!string.IsNullOrWhiteSpace(scope))
        {
            form["scope"] = scope;
        }

        var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(form));
        return await ReadTokenResponseAsync(response);
    }

    private async Task DisableBindingAsync(OAuthScenario scenario, string disabledBinding)
    {
        if (disabledBinding == "connection")
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
            await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
            var connection = await dbContext.OAuthConnections
                .Include(x => x.ActiveGrant)
                .SingleAsync();
            connection.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            connection.ActiveGrant!.RevokedAtUtc = connection.RevokedAtUtc;
            connection.ActiveGrant.RevocationReason = "revoked";
            await dbContext.SaveChangesAsync();
            return;
        }

        if (disabledBinding == "user")
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<BoardOil.Abstractions.DataAccess.IDbContextFactory>();
            await using var dbContext = factory.CreateDbContext<BoardOilDbContext>();
            var user = await dbContext.Users.SingleAsync(x => x.Id == scenario.UserId);
            user.IsActive = false;
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
        int UserId,
        string OAuthClientId,
        string RedirectUri,
        string Resource,
        string PublicBaseUrl,
        string ConnectionName,
        string[] ApprovalScopes);

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

}

public sealed class OAuthAuthorizationFlowFixture : IAsyncLifetime, IResettableApiFactoryFixture
{
    public OAuthAuthorizationFlowFixture()
    {
        DatabasePath = ApiFactoryIntegrationTestBase.BuildDbPath(nameof(OAuthAuthorizationFlowIntegrationTests));
        Factory = new BoardOilApiFactory(
            DatabasePath,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(TimeProvider);
                OAuthTestServiceConfiguration.DisableDynamicClientRegistrationRateLimit(
                    services,
                    "oauth-authorization-flow-tests");
            });
    }

    public string DatabasePath { get; }
    public BoardOilApiFactory Factory { get; }
    internal ManualTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    public ValueTask InitializeAsync()
    {
        using var client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync()
    {
        TimeProvider.Reset();
        Factory.ResetDatabaseFromTemplate();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset currentUtc = utcNow;

    public override DateTimeOffset GetUtcNow() => currentUtc;

    public void Advance(TimeSpan duration) => currentUtc = currentUtc.Add(duration);

    public void Reset() => currentUtc = DateTimeOffset.UtcNow;
}
