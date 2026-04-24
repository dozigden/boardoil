using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Column;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BoardOil.Api.Tests;

public abstract class AuthAuthorisationIntegrationTestBase : IAsyncLifetime
{
    protected string DatabasePath { get; private set; } = string.Empty;
    protected BoardOilApiFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        DatabasePath = BuildDbPath("boardoil-auth-authorisation-tests");
        Factory = new BoardOilApiFactory(DatabasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
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

    protected static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    protected static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest(userName, $"{userName}@localhost", password, role));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardOil.Contracts.Users.ManagedUserDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    protected static async Task CreateClientAccountAsAdminAsync(HttpClient adminClient, string userName, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/client-accounts",
            new CreateClientAccountRequest(userName, $"{userName}@localhost", role));
        response.EnsureSuccessStatusCode();
    }

    protected static async Task AddBoardMemberAsAdminAsync(HttpClient adminClient, int boardId, int userId, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new AddBoardMemberRequest(userId, role));
        response.EnsureSuccessStatusCode();
    }

    protected static async Task<int> CreateColumnAsAdminAsync(HttpClient adminClient, string title)
    {
        var response = await adminClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest(title));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    protected static async Task LoginAsAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
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

    protected sealed record RegisterInitialAdminRequest(string UserName, string Email, string Password);
    protected sealed record LoginRequest(string UserName, string Password);
    protected sealed record CreateUserRequest(string UserName, string Email, string Password, string Role);
    protected sealed record ResetUserPasswordRequest(string NewPassword);
    protected sealed record CreateClientAccountRequest(string UserName, string Email, string Role);
    protected sealed record UpdateUserRequest(string Email, string Role, bool IsActive);
    protected sealed record UpdateClientAccountRequest(string Email, string Role, bool IsActive);
    protected sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
    protected sealed record AuthSessionEnvelope(string CsrfToken);
    protected sealed record ConfigurationEnvelope(bool AllowInsecureCookies, string? McpPublicBaseUrl);
    protected sealed record UserDirectoryEntryEnvelope(int Id, string UserName, bool IsActive);
    protected sealed record ManagedUserEnvelope(int Id, string UserName, string Email, string Role, string IdentityType, bool IsActive);
    protected sealed record ClientAccountEnvelope(int Id, string UserName, string Email, string Role, bool IsActive);
    protected sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
