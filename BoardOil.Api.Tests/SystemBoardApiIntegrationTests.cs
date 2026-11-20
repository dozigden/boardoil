using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Jobs;
using BoardOil.Contracts.Users;
using BoardOil.Services.Jobs;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task DeletedBoardEndpoints_ShouldRequireSystemAdminEvenForBoardOwner()
    {
        var admin = CreateClient();
        var owner = CreateClient();
        await RegisterInitialAdminAsync(admin);
        await CreateUserAsAdminAsync(admin, "owner", "Password1234!", "Standard");
        await LoginAsAsync(owner, "owner", "Password1234!");
        using var created = await owner.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Recoverable board"));
        created.EnsureSuccessStatusCode();
        var board = (await created.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>())!.Data!;
        using var deleted = await owner.DeleteAsync($"/api/boards/{board.Id}");
        deleted.EnsureSuccessStatusCode();
        var restorePath = $"/api/system/boards/{board.Id}/restore";
        var purgePath = $"/api/system/boards/{board.Id}/purge";

        using var ownerList = await owner.GetAsync("/api/system/boards/deleted");
        using var ownerRestore = await owner.PostAsync(restorePath, null);
        using var ownerPurge = await owner.PostAsync(purgePath, null);
        admin.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        using var purgeWithoutCsrf = await admin.PostAsync(purgePath, null);
        await AdminAuthenticationHelper.SetCsrfHeaderAsync(admin);
        var adminList = await admin.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<DeletedBoardSummaryDto>>>("/api/system/boards/deleted");
        using var adminRestore = await admin.PostAsync(restorePath, null);

        Assert.Equal(HttpStatusCode.Forbidden, ownerList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ownerRestore.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ownerPurge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, purgeWithoutCsrf.StatusCode);
        var recoverable = Assert.Single(adminList!.Data!);
        Assert.Equal(board.Id, recoverable.Id);
        Assert.Equal(board.Name, recoverable.Name);
        Assert.Equal(DateTimeKind.Utc, recoverable.DeletedAtUtc.Kind);
        Assert.Equal(HttpStatusCode.OK, adminRestore.StatusCode);
        var restored = (await adminRestore.Content.ReadFromJsonAsync<ApiEnvelope<SystemBoardSummaryDto>>())!.Data!;
        Assert.Equal(board.Id, restored.Id);
    }

    [Fact]
    public async Task Purge_ShouldEnqueueOnlyTheSelectedBoardBeforeItsRetentionPeriodEnds()
    {
        var admin = CreateClient();
        await RegisterInitialAdminAsync(admin);
        var selected = await CreateDeletedBoardAsync(admin, "Purge early");
        var other = await CreateDeletedBoardAsync(admin, "Keep recoverable");

        using var response = await admin.PostAsync($"/api/system/boards/{selected}/purge", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<ApiEnvelope<JobDto>>())!.Data!;
        Assert.Equal(PurgeBoardJobHandler.JobType, job.Type);
        Assert.Equal(JobStatuses.Pending, job.Status);
        Assert.True(job.UserId > 0);
        Assert.Equal(selected, JsonSerializer.Deserialize<PurgeBoardJobPayload>(job.PayloadJson)!.BoardId);
        var beforeRun = await admin.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<DeletedBoardSummaryDto>>>("/api/system/boards/deleted");
        Assert.Equal(new[] { selected, other }, beforeRun!.Data!.Select(board => board.Id).Order());

        using var scope = Factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
        Assert.True(await runner.RunNextDueAsync());
        Assert.False(await runner.RunNextDueAsync());
        var afterRun = await admin.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<DeletedBoardSummaryDto>>>("/api/system/boards/deleted");
        Assert.Equal(other, Assert.Single(afterRun!.Data!).Id);
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

    private static async Task<int> CreateDeletedBoardAsync(HttpClient client, string name)
    {
        using var created = await client.PostAsJsonAsync("/api/boards", new CreateBoardRequest(name));
        created.EnsureSuccessStatusCode();
        var boardId = (await created.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>())!.Data!.Id;
        using var deleted = await client.DeleteAsync($"/api/boards/{boardId}");
        deleted.EnsureSuccessStatusCode();
        return boardId;
    }

    private static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest(userName, userName, $"{userName}@localhost", password, role));
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
        await AdminAuthenticationHelper.SetCsrfHeaderAsync(client);
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateUserRequest(string UserName, string DisplayName, string Email, string Password, string Role);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
