using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SystemJobApiIntegrationTests : TestBaseIntegration
{
    [Fact]
    public async Task AdminHub_ShouldReceiveCommittedJobIdWithoutJobPayload()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress!, "/hubs/system-jobs"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(HubAccessToken);
            })
            .Build();
        var received = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<int?>("JobsChanged", id => received.TrySetResult(id));
        await connection.StartAsync();

        using var scope = Factory.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<IJobService>()
            .EnqueueAsync(new CreateJobRequest("sample", "{\"secret\":\"private\"}"));
        Assert.True(created.Success);
        Assert.Equal(created.Data!.Id, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task HistoryRoutes_ShouldReturnPagedContractsAndValidateRequests()
    {
        int id;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>()
                .CreateDbContext<BoardOilDbContext>();
            await using (db)
            {
                var job = new EntityJob
                {
                    Type = "history.purge",
                    Status = JobStatus.Completed,
                    RunAfterUtc = DateTime.UtcNow.AddDays(1),
                    StartedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow,
                    ResultJson = "{\"deletedCount\":2}",
                    Logs = [new EntityJobLog { Level = JobLogLevel.Info, Message = "Finished", LoggedAtUtc = DateTime.UtcNow }]
                };
                db.Jobs.Add(job);
                await db.SaveChangesAsync();
                id = job.Id;
            }
        }

        var listResponse = await Client.GetAsync("/api/system/jobs?offset=0&limit=1");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResult<JobListDto>>();
        var detailResponse = await Client.GetAsync($"/api/system/jobs/{id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResult<JobDetailsDto>>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(list!.Data!.Items, item => item.Id == id);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal("Finished", Assert.Single(detail!.Data!.Logs).Message);
        Assert.Equal("{\"deletedCount\":2}", detail.Data.ResultJson);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Client.GetAsync("/api/system/jobs?offset=-1&limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Client.GetAsync("/api/system/jobs?offset=0&limit=201")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync("/api/system/jobs/99999999")).StatusCode);
    }

    [Fact]
    public async Task HistoryAndHub_ShouldRequireAdministrator()
    {
        var anonymous = CreateClient();
        var standard = CreateClient();
        var created = await Client.PostAsJsonAsync("/api/system/users",
            new CreateUserRequest("job-reader", "Job reader", "job-reader@localhost", "Password1234!", "Standard"));
        created.EnsureSuccessStatusCode();
        var login = await standard.PostAsJsonAsync("/api/auth/login", new LoginRequest("job-reader", "Password1234!"));
        login.EnsureSuccessStatusCode();

        foreach (var path in new[] { "/api/system/jobs", "/api/system/jobs/1" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync(path)).StatusCode);
        }

        const string negotiate = "/hubs/system-jobs/negotiate?negotiateVersion=1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(negotiate, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await standard.PostAsync(negotiate, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync(negotiate, null)).StatusCode);
    }

    private sealed record CreateUserRequest(string UserName, string DisplayName, string Email, string Password, string Role);
    private sealed record LoginRequest(string UserName, string Password);
}
