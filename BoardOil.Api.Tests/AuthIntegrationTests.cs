using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-auth-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterInitialAdmin_ShouldSucceedOnce_ThenConflict()
    {
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        var firstEnvelope = await first.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(firstEnvelope);
        Assert.NotNull(firstEnvelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", firstEnvelope.Data!.CsrfToken);

        var second = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin2", "Password1234!"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task BootstrapStatus_WhenNoUsers_ShouldRequireInitialAdminSetup()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.True(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task BootstrapStatus_AfterInitialAdminRegistration_ShouldNotRequireInitialAdminSetup()
    {
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong-password"));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task AuthorizationMatrix_AnonymousAndStandardUser_ShouldRespectPolicies()
    {
        var anonymousClient = _factory.CreateClient();
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();

        await RegisterInitialAdminAsync(adminClient);

        // Anonymous requests must be rejected for protected routes.
        var anonymousBoard = await anonymousClient.GetAsync("/api/board");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousBoard.StatusCode);

        // Admin can create a standard user and a column.
        var createUser = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest("member", "Password1234!", "Standard"));
        createUser.EnsureSuccessStatusCode();

        var createColumn = await adminClient.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createColumn.EnsureSuccessStatusCode();
        var createdColumnEnvelope = await createColumn.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createdColumnEnvelope);
        Assert.NotNull(createdColumnEnvelope!.Data);
        var columnId = createdColumnEnvelope.Data!.Id;

        // Standard user logs in and receives csrf token.
        var login = await standardClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "Password1234!"));
        login.EnsureSuccessStatusCode();
        var loginEnvelope = await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(loginEnvelope);
        Assert.NotNull(loginEnvelope!.Data);
        standardClient.DefaultRequestHeaders.Add("X-BoardOil-CSRF", loginEnvelope.Data!.CsrfToken);

        var standardBoard = await standardClient.GetAsync("/api/board");
        Assert.Equal(HttpStatusCode.OK, standardBoard.StatusCode);

        var standardCreateCard = await standardClient.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(columnId, "Standard card", "Allowed", null));
        Assert.Equal(HttpStatusCode.Created, standardCreateCard.StatusCode);

        var standardCreateColumn = await standardClient.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Not allowed", null));
        Assert.Equal(HttpStatusCode.Forbidden, standardCreateColumn.StatusCode);

        var standardGetUsers = await standardClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, standardGetUsers.StatusCode);
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string Password, string Role);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record BootstrapStatusEnvelope(bool RequiresInitialAdminSetup);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
