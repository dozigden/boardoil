using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ScheduledJobApiIntegrationTests : TestBaseIntegration
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task List_ShouldExposeDailyAndOnceDefinitions(bool requested)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var state = await db.ScheduledJobSchedulerStates.SingleAsync(x => x.Name == "resave-all-boards-checkpoint");
            state.RunRequested = requested;
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/system/scheduled-jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ScheduledJobDto[]>>();
        Assert.Equal(5, result!.Data!.Length);
        var once = Assert.Single(result.Data, item => item.Kind == "once");
        Assert.Equal("resave-all-boards", once.Name);
        Assert.Null(once.DailyTime);
        Assert.Equal(requested, once.NextOccurrenceUtc.HasValue);
        Assert.Null(once.LastEvaluatedAtUtc);
        Assert.All(result.Data.Where(item => item.Kind == "daily"), item =>
        {
            Assert.True(item.Enabled);
            Assert.Equal(new TimeOnly(3, 0), item.DailyTime);
            Assert.Equal("UTC", item.TimeZoneId);
            Assert.NotNull(item.NextOccurrenceUtc);
            Assert.Null(item.LastEvaluatedAtUtc);
            Assert.Null(item.CurrentJob);
            Assert.Null(item.LatestStartedJob);
        });
    }

    [Fact]
    public async Task RunNow_ShouldReturnPersistedPendingJobWithInitiatingAdmin()
    {
        var response = await Client.PostAsync("/api/system/scheduled-jobs/history-purge/runs", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<RunScheduledJobNowResultDto>>();
        Assert.Equal(1, result!.Data!.EnqueuedCount);
        var id = Assert.Single(result.Data.JobIds);
        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        var job = await db.Jobs.SingleAsync(x => x.Id == id);
        var adminId = await db.Users.Where(x => x.UserName == "admin").Select(x => x.Id).SingleAsync();
        Assert.Equal(adminId, job.UserId);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(MaintenanceJobTypes.HistoryPurge, job.Type);
        Assert.StartsWith("adhoc:history-purge:", job.CorrelationId);
        var list = await Client.GetFromJsonAsync<ApiResult<ScheduledJobDto[]>>("/api/system/scheduled-jobs");
        Assert.Equal(id, list!.Data!.Single(x => x.Name == "history-purge").CurrentJob!.Id);
        Assert.Null(list.Data!.Single(x => x.Name == "history-purge").LastEvaluatedAtUtc);
    }

    [Fact]
    public async Task RunNow_WhenUnknown_ShouldReturnNotFound()
    {
        var response = await Client.PostAsync("/api/system/scheduled-jobs/unknown/runs", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunNow_WhenNoOccurrences_ShouldReturnZeroAndNoIds()
    {
        await using var factory = new BoardOilApiFactory(CreateDbPath("empty-schedule"),
            configureTestServices: services => services.AddSingleton<IScheduledJobDefinition, EmptySchedule>());
        using var client = factory.CreateClient();
        await AuthenticateAsInitialAdminAsync(client, factory.Services);

        var response = await client.PostAsync("/api/system/scheduled-jobs/empty/runs", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<RunScheduledJobNowResultDto>>();
        Assert.Equal(0, result!.Data!.EnqueuedCount);
        Assert.Empty(result.Data.JobIds);
    }

    [Fact]
    public async Task TimeZoneOptions_ShouldIncludeDefaultAndLondon()
    {
        var result = await Client.GetFromJsonAsync<ApiResult<SystemTimeZoneOptionsDto>>("/api/system/timezone/options");

        Assert.Equal("UTC", result!.Data!.DefaultId);
        Assert.Contains(result.Data.Options, option => option.Id == "Europe/London");
        Assert.Contains(result.Data.Options, option => option.Id == "UTC");
    }

    [Fact]
    public async Task UpdateTimeZone_ShouldPersistAndAffectScheduleTiming()
    {
        var response = await Client.PutAsJsonAsync("/api/system/timezone", new UpdateSystemTimeZoneRequest("Europe/London"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Client.GetFromJsonAsync<ApiResult<SystemTimeZoneDto>>("/api/system/timezone");
        Assert.Equal("Europe/London", saved!.Data!.SystemTimeZoneId);
        var list = await Client.GetFromJsonAsync<ApiResult<ScheduledJobDto[]>>("/api/system/scheduled-jobs");
        Assert.All(list!.Data!, schedule => Assert.Equal("Europe/London", schedule.TimeZoneId));
    }

    [Fact]
    public async Task UpdateTimeZone_WhenInvalid_ShouldReturnFieldValidation()
    {
        var response = await Client.PutAsJsonAsync("/api/system/timezone", new UpdateSystemTimeZoneRequest("invalid-zone"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<SystemTimeZoneDto>>();
        Assert.NotEmpty(result!.ValidationErrors!["systemTimeZoneId"]);
    }

    [Fact]
    public async Task ScheduleAndTimezoneRoutes_ShouldRequireAdministrator()
    {
        var anonymous = CreateClient();
        var standard = CreateClient();
        (await Client.PostAsJsonAsync("/api/system/users", new
        {
            userName = "schedule-reader", displayName = "Schedule reader", email = "schedule@localhost",
            password = "Password1234!", role = "Standard"
        })).EnsureSuccessStatusCode();
        (await standard.PostAsJsonAsync("/api/auth/login", new { userName = "schedule-reader", password = "Password1234!" }))
            .EnsureSuccessStatusCode();
        await AdminAuthenticationHelper.SetCsrfHeaderAsync(standard);

        foreach (var (client, expected) in new[] { (anonymous, HttpStatusCode.Unauthorized), (standard, HttpStatusCode.Forbidden) })
        {
            Assert.Equal(expected, (await client.GetAsync("/api/system/scheduled-jobs")).StatusCode);
            Assert.Equal(expected, (await client.PostAsync("/api/system/scheduled-jobs/history-purge/runs", null)).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/system/timezone")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/system/timezone/options")).StatusCode);
            Assert.Equal(expected, (await client.PutAsJsonAsync("/api/system/timezone", new UpdateSystemTimeZoneRequest("UTC"))).StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserMutation_WithoutCsrf_ShouldBeRejected(bool timezone)
    {
        Client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        var response = timezone
            ? await Client.PutAsJsonAsync("/api/system/timezone", new UpdateSystemTimeZoneRequest("UTC"))
            : await Client.PostAsync("/api/system/scheduled-jobs/history-purge/runs", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult>();
        Assert.Equal("CSRF validation failed.", result!.Message);
    }

    private sealed class EmptySchedule : IScheduledJobDefinition
    {
        public string Name => "empty";
        public string DisplayName => "Empty schedule";
        public string SchedulerStateName => "empty-checkpoint";
        public Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScheduledJobDefinitionConfiguration(true, new TimeOnly(3, 0)));
        public Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(DateTime dueAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScheduledJobOccurrence>>([]);
    }
}
