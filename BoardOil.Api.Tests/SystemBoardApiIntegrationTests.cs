using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Users;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SystemBoardApiIntegrationTests : ApiFactoryIntegrationTestBase
{

    [Fact]
    public async Task Admin_GetSystemBoards_ShouldReturnSuccessContract()
    {
        // Arrange
        var adminClient = CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var systemScoped = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SystemBoardSummaryDto>>>("/api/system/boards");

        // Assert
        Assert.NotNull(systemScoped);
        Assert.NotNull(systemScoped!.Data);
        Assert.True(systemScoped.Success);
        Assert.NotEmpty(systemScoped.Data!);
    }

    [Fact]
    public async Task StandardUser_GetSystemBoards_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = CreateClient();
        var memberClient = CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "member", "Password1234!");

        // Act
        var response = await memberClient.GetAsync("/api/system/boards");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_SystemMembershipEndpoints_ShouldAddAndListMembersWithoutAdminBoardMembership()
    {
        // Arrange
        var adminClient = CreateClient();
        var memberClient = CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        _ = await CreateUserAsAdminAsync(adminClient, "owner", "Password1234!", "Standard");
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

        var membersEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardMemberDto>>>(
            $"/api/system/boards/{boardId}/members");

        // Assert
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        Assert.NotNull(addEnvelope);
        Assert.NotNull(addEnvelope!.Data);
        Assert.Equal(helperUserId, addEnvelope.Data!.UserId);
        Assert.Equal("Contributor", addEnvelope.Data.Role);

        Assert.NotNull(membersEnvelope);
        Assert.NotNull(membersEnvelope!.Data);
        Assert.Contains(membersEnvelope.Data!, x => x.UserId == helperUserId && x.Role == "Contributor");
    }

    private async Task RegisterInitialAdminAsync(HttpClient client)
    {
        _ = await AuthenticateAsInitialAdminAsync(client);
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

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string Email, string Password, string Role);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
