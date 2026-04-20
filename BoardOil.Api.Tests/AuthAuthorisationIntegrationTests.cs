using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthAuthorisationIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-auth-authorisation-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
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
    public async Task StandardUser_WithoutBoardMembership_GetBoard_ShouldReturnForbidden()
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
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_WithoutBoardMembership_ArchiveCard_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        var createdColumnResponse = await adminClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumnEnvelope = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createdColumnEnvelope);
        Assert.NotNull(createdColumnEnvelope!.Data);
        var createdCardResponse = await adminClient.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumnEnvelope.Data!.Id, "Archive target", "Desc", null));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsync($"/api/boards/1/cards/{createdCardEnvelope.Data!.Id}/archive", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_WithoutBoardMembership_ArchiveCardsBulk_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        var createdColumnResponse = await adminClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumnEnvelope = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createdColumnEnvelope);
        Assert.NotNull(createdColumnEnvelope!.Data);
        var createdCardResponse = await adminClient.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumnEnvelope.Data!.Id, "Archive target", "Desc", null));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/cards/archive",
            new ArchiveCardsRequest([createdCardEnvelope.Data!.Id]));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_WithoutBoardMembership_GetArchivedCards_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1/cards/archived");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateCard_ShouldReturnCreated()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
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
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
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
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1/tags");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetCardTypes_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1/card-types");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateCardType_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Feature"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_UpdateAndDeleteCardType_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        var createTypeResponse = await adminClient.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Feature"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>();
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);
        var cardTypeId = createdTypeEnvelope.Data!.Id;
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var updateResponse = await standardClient.PutAsJsonAsync(
            $"/api/boards/1/card-types/{cardTypeId}",
            new UpdateCardTypeRequest("Platform"));
        var setDefaultResponse = await standardClient.PatchAsync($"/api/boards/1/card-types/{cardTypeId}/default", null);
        var deleteResponse = await standardClient.DeleteAsync($"/api/boards/1/card-types/{cardTypeId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, setDefaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task StandardUser_UpdateTagStyle_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await SeedTagAsync("member", "MEMBER", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        await LoginAsAsync(standardClient, "member", "Password1234!");
        var tagsEnvelope = await standardClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags");
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var memberTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "member");

        // Act
        var response = await standardClient.PutAsJsonAsync(
            $"/api/boards/1/tags/{memberTag.Id}",
            new UpdateTagStyleRequest(
                Name: "member",
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
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Not allowed"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_GetUsers_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/users");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<UserDirectoryEntryEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Contains(envelope.Data!, x => x.UserName == "member");
    }

    [Fact]
    public async Task StandardUser_GetUsers_ShouldIncludeClientAccounts()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await CreateClientAccountAsAdminAsync(adminClient, "client-bot", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/users");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<UserDirectoryEntryEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Contains(envelope.Data!, x => x.UserName == "member");
        Assert.Contains(envelope.Data!, x => x.UserName == "client-bot");
    }

    [Fact]
    public async Task AdminUser_GetSystemUsers_ShouldExcludeClientAccounts()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await CreateClientAccountAsAdminAsync(adminClient, "client-bot", "Standard");

        // Act
        var response = await adminClient.GetAsync("/api/system/users");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<ManagedUserEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Contains(envelope.Data!, x => x.UserName == "member");
        Assert.DoesNotContain(envelope.Data!, x => x.UserName == "client-bot");
    }

    [Fact]
    public async Task StandardUser_GetAdminUsers_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/system/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUser_ResetUserPassword_ShouldAllowLoginWithNewPasswordOnly()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var memberClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "member", "Password1234!");

        // Act
        var resetResponse = await adminClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("FreshPassword1234!"));
        var oldLoginResponse = await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "Password1234!"));
        var newLoginResponse = await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "FreshPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task StandardUser_ResetUserPassword_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("FreshPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUser_ResetUserPassword_WithInvalidNewPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("short"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        var response = await standardClient.GetAsync("/api/system/configuration");

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
        var adminClient = _factory.CreateClient();
        var standardClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));

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
        var putResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com/"));
        var putEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();
        var getResponse = await adminClient.GetAsync("/api/system/configuration");
        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConfigurationEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.NotNull(putEnvelope);
        Assert.NotNull(putEnvelope!.Data);
        Assert.Equal("https://boardoil.example.com", putEnvelope.Data!.McpPublicBaseUrl);
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
        var seedResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com"));
        seedResponse.EnsureSuccessStatusCode();

        // Act
        var clearResponse = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest(null));
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
        var response = await adminClient.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest(invalidValue));
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

    private static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync("/api/system/users", new CreateUserRequest(userName, password, role));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardOil.Contracts.Users.ManagedUserDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    private static async Task CreateClientAccountAsAdminAsync(HttpClient adminClient, string userName, string role)
    {
        var response = await adminClient.PostAsJsonAsync("/api/system/client-accounts", new CreateClientAccountRequest(userName, role));
        response.EnsureSuccessStatusCode();
    }

    private static async Task AddBoardMemberAsAdminAsync(HttpClient adminClient, int boardId, int userId, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"/api/boards/{boardId}/members",
            new AddBoardMemberRequest(userId, role));
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

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string Password, string Role);
    private sealed record ResetUserPasswordRequest(string NewPassword);
    private sealed record CreateClientAccountRequest(string UserName, string Role);
    private sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ConfigurationEnvelope(bool AllowInsecureCookies, string? McpPublicBaseUrl);
    private sealed record UserDirectoryEntryEnvelope(int Id, string UserName, bool IsActive);
    private sealed record ManagedUserEnvelope(string UserName);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
