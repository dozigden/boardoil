using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Microsoft.Data.Sqlite;
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
    public async Task RegisterInitialAdmin_FirstAttempt_ShouldReturnCreated()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenAdminAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin2", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WithStaleAccessCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "boardoil_access=stale-token");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BootstrapStatus_WhenNoUsers_ShouldRequireInitialAdminSetup()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.True(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task BootstrapStatus_AfterInitialAdminRegistration_ShouldNotRequireInitialAdminSetup()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong-password"));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task Login_WithExistingAuthCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenInsecureCookiesDisabled_ShouldSetSecureFlagOnAuthCookies()
    {
        // Arrange
        var dbPath = BuildDbPath("boardoil-auth-secure-cookie-tests");
        await using var factory = new BoardOilApiFactory(dbPath, allowInsecureCookies: false);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.True(ResponseHasCookieAttribute(response, "boardoil_access", "secure"));
        Assert.True(ResponseHasCookieAttribute(response, "boardoil_refresh", "secure"));
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenInsecureCookiesEnabled_ShouldNotSetSecureFlagOnAuthCookies()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_access", "secure"));
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_refresh", "secure"));
    }

    [Fact]
    public async Task AnonymousUser_GetBoard_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetBoard_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateCard_ShouldReturnCreated()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        var columnId = await CreateColumnAsAdminAsync(adminClient, "Todo");
        await LoginAsAsync(standardClient, "member", "Password1234!");
        var createTagResponse = await standardClient.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("member"));
        createTagResponse.EnsureSuccessStatusCode();

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnId, "Standard card", "Allowed", ["member"]));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateTag_ShouldReturnCreated()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/tags",
            new CreateTagRequest("member"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetTags_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1/tags");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_PatchTagStyle_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await SeedTagAsync("member", "MEMBER", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        await LoginAsAsync(standardClient, "member", "Password1234!");
        var tagsEnvelope = await standardClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags");
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var memberTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "member");

        // Act
        var response = await standardClient.PatchAsJsonAsync(
            $"/api/boards/1/tags/{memberTag.Id}",
            new UpdateTagStyleRequest(
                StyleName: "solid",
                StylePropertiesJson: """{"backgroundColor":"#113355","textColorMode":"auto"}"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateColumn_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Not allowed"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetUsers_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetConfiguration_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/configuration");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUser_GetConfiguration_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.GetAsync("/api/configuration");
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
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PatchAsJsonAsync("/api/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUser_UpdateConfiguration_WithValidMcpPublicBaseUrl_ShouldPersist()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var patchResponse = await adminClient.PatchAsJsonAsync("/api/configuration", new UpdateConfigurationRequest("https://boardoil.example.com/"));
        var patchEnvelope = await patchResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();
        var getResponse = await adminClient.GetAsync("/api/configuration");
        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        Assert.NotNull(patchEnvelope);
        Assert.NotNull(patchEnvelope!.Data);
        Assert.Equal("https://boardoil.example.com", patchEnvelope.Data!.McpPublicBaseUrl);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getEnvelope);
        Assert.NotNull(getEnvelope!.Data);
        Assert.Equal("https://boardoil.example.com", getEnvelope.Data!.McpPublicBaseUrl);
    }

    [Fact]
    public async Task AdminUser_UpdateConfiguration_WithNullMcpPublicBaseUrl_ShouldClearOverride()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var seedResponse = await adminClient.PatchAsJsonAsync("/api/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));
        seedResponse.EnsureSuccessStatusCode();

        // Act
        var clearResponse = await adminClient.PatchAsJsonAsync("/api/configuration", new UpdateConfigurationRequest(null));
        var clearEnvelope = await clearResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.NotNull(clearEnvelope);
        Assert.NotNull(clearEnvelope!.Data);
        Assert.Null(clearEnvelope.Data!.McpPublicBaseUrl);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://boardoil.example.com")]
    [InlineData("https://boardoil.example.com?x=1")]
    [InlineData("https://boardoil.example.com#frag")]
    public async Task AdminUser_UpdateConfiguration_WithInvalidMcpPublicBaseUrl_ShouldReturnBadRequest(string invalidValue)
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.PatchAsJsonAsync("/api/configuration", new UpdateConfigurationRequest(invalidValue));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
    }

    private async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Tags" ("BoardId", "Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ($boardId, $name, $normalisedName, $styleName, $stylePropertiesJson, $createdAtUtc, $updatedAtUtc);
            """;
        command.Parameters.AddWithValue("$boardId", 1);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$normalisedName", normalisedName);
        command.Parameters.AddWithValue("$styleName", styleName);
        command.Parameters.AddWithValue("$stylePropertiesJson", stylePropertiesJson);
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("$createdAtUtc", now);
        command.Parameters.AddWithValue("$updatedAtUtc", now);
        await command.ExecuteNonQueryAsync();
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

    private static async Task CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(userName, password, role));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> CreateColumnAsAdminAsync(HttpClient adminClient, string title)
    {
        var response = await adminClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest(title));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    private static async Task LoginAsAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    private static bool ResponseHasCookieAttribute(HttpResponseMessage response, string cookieName, string attribute)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return false;
        }

        var prefix = $"{cookieName}=";
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Any(x => x.Equals(attribute, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string Password, string Role);
    private sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ConfigurationEnvelope(bool AllowInsecureCookies, string? McpPublicBaseUrl);
    private sealed record BootstrapStatusEnvelope(bool RequiresInitialAdminSetup);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
