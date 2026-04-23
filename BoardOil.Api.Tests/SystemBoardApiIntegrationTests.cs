using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Users;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SystemBoardApiIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-system-board-api-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Admin_GetSystemBoards_ShouldReturnBoardsOutsideAdminMembership()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var memberClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "member", "Password1234!");
        var createBoardResponse = await memberClient.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Member Board"));
        createBoardResponse.EnsureSuccessStatusCode();
        var createBoardEnvelope = await createBoardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>();
        Assert.NotNull(createBoardEnvelope);
        Assert.NotNull(createBoardEnvelope!.Data);
        var memberBoardId = createBoardEnvelope.Data!.Id;

        // Act
        var userScoped = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryDto>>>("/api/boards");
        var systemScoped = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SystemBoardSummaryDto>>>("/api/system/boards");

        // Assert
        Assert.NotNull(userScoped);
        Assert.NotNull(userScoped!.Data);
        Assert.DoesNotContain(userScoped.Data!, x => x.Id == memberBoardId);

        Assert.NotNull(systemScoped);
        Assert.NotNull(systemScoped!.Data);
        Assert.Contains(systemScoped.Data!, x => x.Id == memberBoardId);
    }

    [Fact]
    public async Task StandardUser_GetSystemBoards_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var memberClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "member", "Password1234!");

        // Act
        var response = await memberClient.GetAsync("/api/system/boards");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_SystemMembershipEndpoints_ShouldManageMembersWithoutAdminBoardMembership()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        var memberClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var ownerUserId = await CreateUserAsAdminAsync(adminClient, "owner", "Password1234!", "Standard");
        var helperUserId = await CreateUserAsAdminAsync(adminClient, "helper", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "owner", "Password1234!");
        var createBoardResponse = await memberClient.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Backstop Board"));
        createBoardResponse.EnsureSuccessStatusCode();
        var createBoardEnvelope = await createBoardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>();
        Assert.NotNull(createBoardEnvelope);
        Assert.NotNull(createBoardEnvelope!.Data);
        var boardId = createBoardEnvelope.Data!.Id;

        // Act
        var addResponse = await adminClient.PostAsJsonAsync(
            $"/api/system/boards/{boardId}/members",
            new AddBoardMemberRequest(helperUserId, "Contributor"));
        var addEnvelope = await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardMemberDto>>();

        var demoteOwnerResponse = await adminClient.PatchAsJsonAsync(
            $"/api/system/boards/{boardId}/members/{ownerUserId}",
            new UpdateBoardMemberRoleRequest("Contributor"));
        var demoteEnvelope = await demoteOwnerResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        var membersEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardMemberDto>>>(
            $"/api/system/boards/{boardId}/members");
        var duplicateAddResponse = await adminClient.PostAsJsonAsync(
            $"/api/system/boards/{boardId}/members",
            new AddBoardMemberRequest(helperUserId, "Owner"));
        var duplicateAddEnvelope = await duplicateAddResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        Assert.NotNull(addEnvelope);
        Assert.NotNull(addEnvelope!.Data);
        Assert.Equal(helperUserId, addEnvelope.Data!.UserId);
        Assert.Equal("Contributor", addEnvelope.Data.Role);

        Assert.Equal(HttpStatusCode.BadRequest, demoteOwnerResponse.StatusCode);
        Assert.NotNull(demoteEnvelope);
        Assert.False(demoteEnvelope!.Success);
        Assert.Equal("Board must have at least one owner.", demoteEnvelope.Message);

        Assert.NotNull(membersEnvelope);
        Assert.NotNull(membersEnvelope!.Data);
        Assert.Contains(membersEnvelope.Data!, x => x.UserId == helperUserId && x.Role == "Contributor");

        Assert.Equal(HttpStatusCode.BadRequest, duplicateAddResponse.StatusCode);
        Assert.NotNull(duplicateAddEnvelope);
        Assert.False(duplicateAddEnvelope!.Success);
        Assert.Equal("User is already a board member.", duplicateAddEnvelope.Message);
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
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

    private static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest(userName, $"{userName}@localhost", password, role));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ManagedUserDto>>();
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

    private sealed record RegisterInitialAdminRequest(string UserName, string Email, string Password);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string Email, string Password, string Role);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
