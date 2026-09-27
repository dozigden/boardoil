using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ConfigurationTimeZoneIntegrationTests : TestBaseIntegration
{
    private static readonly DateTime Baseline = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Save_ShouldPersistAllSettingsAndResetScheduleBaselineTogether()
    {
        await SeedAsync();

        var response = await Client.PutAsJsonAsync("/api/system/configuration",
            new UpdateConfigurationRequest("https://new.example.com/", true, "Europe/London"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Client.GetFromJsonAsync<ApiResult<ConfigurationDto>>("/api/system/configuration");
        Assert.Equal("https://new.example.com", saved!.Data!.McpPublicBaseUrl);
        Assert.True(saved.Data.OAuthLifecycleDiagnosticsEnabled);
        Assert.Equal("Europe/London", saved.Data.SystemTimeZoneId);
        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        var state = await db.ScheduledJobSchedulerStates.SingleAsync();
        Assert.True(state.LastRunTimeUtc > Baseline);
        Assert.Equal(Baseline, state.PendingDueAtUtc);
        Assert.Equal("Europe/London", (await db.AppSettings.SingleAsync(x => x.Key == "system_timezone")).Value);
        Assert.Equal("true", (await db.AppSettings.SingleAsync(x => x.Key == "oauth_lifecycle_diagnostics_enabled")).Value);
    }

    [Theory]
    [InlineData("https://new.example.com", "invalid-zone")]
    [InlineData("relative/path", "Europe/London")]
    public async Task Save_WhenAnyFieldInvalid_ShouldLeaveAllSettingsUnchanged(string url, string zone)
    {
        await SeedAsync();

        var response = await Client.PutAsJsonAsync("/api/system/configuration",
            new UpdateConfigurationRequest(url, true, zone));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertOriginalStateAsync();
    }

    [Fact]
    public async Task Save_WhenDatabaseWriteFails_ShouldRollBackSettingsAndScheduleBaseline()
    {
        await SeedAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_configuration_timezone BEFORE UPDATE ON AppSettings
                WHEN NEW.Key = 'system_timezone'
                BEGIN SELECT RAISE(ABORT, 'Test configuration save failure'); END;
                """);
        }

        var response = await Client.PutAsJsonAsync("/api/system/configuration",
            new UpdateConfigurationRequest("https://new.example.com", true, "Europe/London"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertOriginalStateAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_WhenTimezoneUnchanged_ShouldPreserveScheduleBaseline(bool hasSavedTimezone)
    {
        await SeedAsync();
        if (!hasSavedTimezone)
        {
            using var setup = Factory.Services.CreateScope();
            await using var setupDb = setup.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            setupDb.AppSettings.Remove(await setupDb.AppSettings.SingleAsync(x => x.Key == "system_timezone"));
            await setupDb.SaveChangesAsync();
        }

        var response = await Client.PutAsJsonAsync("/api/system/configuration",
            new UpdateConfigurationRequest("https://new.example.com", true, "UTC"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        Assert.Equal(Baseline, (await db.ScheduledJobSchedulerStates.SingleAsync()).LastRunTimeUtc);
    }

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        db.AppSettings.AddRange(
            new EntityAppSetting { Key = "mcp_public_base_url", Value = "https://old.example.com" },
            new EntityAppSetting { Key = "system_timezone", Value = "UTC" },
            new EntityAppSetting { Key = "oauth_lifecycle_diagnostics_enabled", Value = "false" });
        db.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
        {
            Name = "test-checkpoint", LastRunTimeUtc = Baseline, PendingDueAtUtc = Baseline
        });
        await db.SaveChangesAsync();
    }

    private async Task AssertOriginalStateAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        Assert.Equal("https://old.example.com", (await db.AppSettings.SingleAsync(x => x.Key == "mcp_public_base_url")).Value);
        Assert.Equal("UTC", (await db.AppSettings.SingleAsync(x => x.Key == "system_timezone")).Value);
        Assert.Equal("false", (await db.AppSettings.SingleAsync(x => x.Key == "oauth_lifecycle_diagnostics_enabled")).Value);
        var state = await db.ScheduledJobSchedulerStates.SingleAsync();
        Assert.Equal(Baseline, state.LastRunTimeUtc);
        Assert.Equal(Baseline, state.PendingDueAtUtc);
        Assert.False(scope.ServiceProvider.GetRequiredService<OAuthTokenAuditCaptureState>().IsEnabled);
    }
}
