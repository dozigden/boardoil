using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.ErrorLogs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ClientErrorReportApiIntegrationTests : TestBaseIntegration
{
    private const string Endpoint = "/api/system/error-logs:report-client-error";

    [Fact]
    public async Task ReportClientError_ShouldPersistActorAndReturnErrorLogContract()
    {
        // Arrange
        var request = NewRequest();

        // Act
        var response = await Client.PostAsJsonAsync(
            Endpoint,
            request,
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ErrorLogDto>>(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(ErrorLogSources.Frontend, result!.Data!.Source);
        Assert.Equal(ErrorLogAreas.WebClient, result.Data.Area);
        Assert.NotNull(result.Data.ActorUserId);
        await using var dbContext = CreateDbContext();
        var errorLog = await dbContext.ErrorLogs.SingleAsync(
            x => x.Id == result.Data.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.ActorUserId, errorLog.ActorUserId);
        Assert.Equal("/boards/1?search=books&token=[redacted]", errorLog.RequestPath);
    }

    [Fact]
    public async Task ReportClientError_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        using var anonymousClient = Factory.CreateClient();

        // Act
        var response = await anonymousClient.PostAsJsonAsync(
            Endpoint,
            NewRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var dbContext = CreateDbContext();
        Assert.Empty(dbContext.ErrorLogs);
    }

    [Fact]
    public async Task ReportClientError_WhenRateLimitExceeded_ShouldReturnTooManyRequests()
    {
        // Arrange
        HttpResponseMessage? response = null;

        // Act
        for (var index = 0; index <= ErrorLogRateLimitExtensions.ClientErrorReportsPerMinute; index++)
        {
            response?.Dispose();
            response = await Client.PostAsJsonAsync(
                Endpoint,
                NewRequest() with { Message = $"client error {index}" },
                TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.NotNull(response);
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private BoardOilDbContext CreateDbContext()
    {
        using var scope = Factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        return factory.CreateDbContext<BoardOilDbContext>();
    }

    private static ClientErrorReportRequest NewRequest() =>
        new(
            "client render failed",
            "TypeError",
            "at render",
            "vue",
            "board",
            "/boards/1?search=books&token=never-store",
            "1.4.0 (dev/local) abc123",
            new ClientErrorViewportDto(1280, 720),
            "Test Browser",
            JsonSerializer.Deserialize<JsonElement>("""{"componentName":"BoardView"}"""));
}
