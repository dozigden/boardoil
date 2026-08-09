using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.Configuration;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ErrorLogApiIntegrationTests : TestBaseIntegration
{
    [Fact]
    public async Task UnhandledApiException_ShouldPersistErrorAndReturnGenericReference()
    {
        // Act
        var response = await Client.GetAsync(
            "/api/system/configuration",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResult>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("Error reference", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ThrowingConfigurationService.ExceptionMessage, result.Message, StringComparison.Ordinal);

        var errorLog = await GetSingleErrorLogAsync();
        Assert.Equal(ErrorLogSources.Backend, errorLog.Source);
        Assert.Equal(ErrorLogAreas.ApiRequest, errorLog.Area);
        Assert.Equal(typeof(InvalidOperationException).FullName, errorLog.ExceptionType);
        Assert.Equal(ThrowingConfigurationService.ExceptionMessage, errorLog.Message);
        Assert.Equal("GET", errorLog.RequestMethod);
        Assert.Equal("/api/system/configuration", errorLog.RequestPath);
        Assert.NotNull(errorLog.ActorUserId);
        Assert.NotNull(errorLog.TraceIdentifier);
        Assert.Contains(errorLog.Id.ToString(), result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAndGet_ShouldReturnAdministratorErrorLogContracts()
    {
        // Arrange
        await AddErrorLogsAsync(
            NewErrorLog("first", DateTime.UtcNow.AddMinutes(-10)),
            NewErrorLog("second", DateTime.UtcNow));

        // Act
        var listResponse = await Client.GetAsync(
            "/api/system/error-logs?offset=0&limit=1",
            TestContext.Current.CancellationToken);
        var listResult = await listResponse.Content.ReadFromJsonAsync<ApiResult<ErrorLogListDto>>(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listResult?.Data);
        Assert.Equal(2, listResult!.Data!.TotalCount);
        var listed = Assert.Single(listResult.Data.Items);
        Assert.Equal("second", listed.Message);

        var detailsResponse = await Client.GetAsync(
            $"/api/system/error-logs/{listed.Id}",
            TestContext.Current.CancellationToken);
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<ApiResult<ErrorLogDetailsDto>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.NotNull(detailsResult?.Data);
        Assert.Equal("stack second", detailsResult!.Data!.StackTrace);
        Assert.Equal("""{"endpoint":"second"}""", detailsResult.Data.ContextJson);
    }

    [Fact]
    public async Task Purge_ShouldDeleteOnlyLogsOlderThanRetentionPeriod()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        await AddErrorLogsAsync(
            NewErrorLog("old", nowUtc.AddDays(-ErrorLogRetention.RetentionDays).AddMinutes(-1)),
            NewErrorLog("kept", nowUtc));

        // Act
        var response = await Client.PostAsync(
            "/api/system/error-logs:purge",
            content: null,
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ErrorLogPurgeResultDto>>(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(ErrorLogRetention.RetentionDays, result!.Data!.RetentionDays);
        Assert.Equal(1, result.Data.DeletedCount);

        await using var dbContext = CreateDbContext();
        var remaining = await dbContext.ErrorLogs
            .Select(x => x.Message)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["kept"], remaining);
    }

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configureTestServices: services =>
            {
                services.RemoveAll<IConfigurationService>();
                services.AddScoped<IConfigurationService, ThrowingConfigurationService>();
            });

    private async Task<EntityErrorLog> GetSingleErrorLogAsync()
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.ErrorLogs.SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddErrorLogsAsync(params EntityErrorLog[] errorLogs)
    {
        await using var dbContext = CreateDbContext();
        dbContext.ErrorLogs.AddRange(errorLogs);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private BoardOilDbContext CreateDbContext()
    {
        using var scope = Factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        return factory.CreateDbContext<BoardOilDbContext>();
    }

    private static EntityErrorLog NewErrorLog(string message, DateTime occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Source = ErrorLogSources.Backend,
            Area = ErrorLogAreas.ApiRequest,
            ExceptionType = typeof(InvalidOperationException).FullName!,
            Message = message,
            StackTrace = $"stack {message}",
            TraceIdentifier = $"trace-{message}",
            RequestMethod = "GET",
            RequestPath = "/api/test",
            ContextJson = $$"""{"endpoint":"{{message}}"}"""
        };

    private sealed class ThrowingConfigurationService : IConfigurationService
    {
        public const string ExceptionMessage = "configuration failed with private details";

        public Task<ApiResult<ConfigurationDto>> GetConfigurationAsync() =>
            throw new InvalidOperationException(ExceptionMessage);

        public Task<ApiResult<ConfigurationDto>> UpdateConfigurationAsync(UpdateConfigurationRequest request) =>
            throw new NotSupportedException();

        public Task<string?> GetMcpPublicBaseUrlAsync() =>
            Task.FromResult<string?>(null);
    }
}
