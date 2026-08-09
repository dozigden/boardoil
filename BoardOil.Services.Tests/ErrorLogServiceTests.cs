using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ErrorLogServiceTests : TestBaseDb
{
    private static readonly DateTime FixedNowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListAsync_ShouldReturnPagedErrorLogsNewestFirst()
    {
        // Arrange
        DbContextForArrange.ErrorLogs.AddRange(
            NewErrorLog("first", FixedNowUtc.AddMinutes(-10)),
            NewErrorLog("second", FixedNowUtc.AddMinutes(-5)),
            NewErrorLog("third", FixedNowUtc));
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        // Act
        var result = await service.ListAsync(1, 2);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(1, result.Data.Offset);
        Assert.Equal(2, result.Data.Limit);
        Assert.Collection(
            result.Data.Items,
            item => Assert.Equal("second", item.Message),
            item => Assert.Equal("first", item.Message));
    }

    [Theory]
    [InlineData(-1, 10, "offset")]
    [InlineData(0, 0, "limit")]
    [InlineData(0, 201, "limit")]
    public async Task ListAsync_WithInvalidPagination_ShouldReturnBadRequest(
        int offset,
        int limit,
        string expectedProperty)
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();

        // Act
        var result = await service.ListAsync(offset, limit);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(expectedProperty, result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnFullErrorLogDetails()
    {
        // Arrange
        var entity = NewErrorLog("failed", FixedNowUtc);
        entity.StackTrace = "stack details";
        entity.ContextJson = """{"endpoint":"test"}""";
        DbContextForArrange.ErrorLogs.Add(entity);
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        // Act
        var result = await service.GetAsync(entity.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("stack details", result.Data!.StackTrace);
        Assert.Equal("""{"endpoint":"test"}""", result.Data.ContextJson);
    }

    [Fact]
    public async Task GetAsync_WhenMissing_ShouldReturnNotFound()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();

        // Act
        var result = await service.GetAsync(404);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task LogExceptionAsync_ShouldPersistSanitisedDiagnosticDetails()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var exception = new InvalidOperationException(
            "request failed with bearer abc123 and password=super-secret");

        // Act
        var id = await service.LogExceptionAsync(
            exception,
            new ErrorLogContext(
                ErrorLogSources.Backend,
                ErrorLogAreas.ApiRequest,
                TraceIdentifier: "trace-1",
                RequestMethod: "POST",
                RequestPath: "/api/test?access_token=secret-value",
                ActorUserId: ActorUserId,
                ContextJson: """{"authorization":"token=private-value"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(id);
        var errorLog = await DbContextForAssert.ErrorLogs.SingleAsync(
            x => x.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal(FixedNowUtc, errorLog.OccurredAtUtc);
        Assert.Equal(ErrorLogSources.Backend, errorLog.Source);
        Assert.Equal(ErrorLogAreas.ApiRequest, errorLog.Area);
        Assert.Equal(typeof(InvalidOperationException).FullName, errorLog.ExceptionType);
        Assert.Contains("bearer [redacted]", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("password=[redacted]", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.Equal("/api/test?access_token=[redacted]", errorLog.RequestPath);
        Assert.DoesNotContain("private-value", errorLog.ContextJson, StringComparison.Ordinal);
        Assert.Equal(ActorUserId, errorLog.ActorUserId);
    }

    [Fact]
    public async Task PurgeExpiredAsync_ShouldDeleteOnlyLogsOlderThanRetentionCutoff()
    {
        // Arrange
        var cutoffUtc = FixedNowUtc.AddDays(-ErrorLogRetention.RetentionDays);
        DbContextForArrange.ErrorLogs.AddRange(
            NewErrorLog("old", cutoffUtc.AddTicks(-1)),
            NewErrorLog("equal", cutoffUtc),
            NewErrorLog("new", cutoffUtc.AddTicks(1)));
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        // Act
        var result = await service.PurgeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(ErrorLogRetention.RetentionDays, result.Data!.RetentionDays);
        Assert.Equal(cutoffUtc, result.Data.CutoffUtc);
        Assert.Equal(1, result.Data.DeletedCount);
        var remainingMessages = await DbContextForAssert.ErrorLogs
            .OrderBy(x => x.Message)
            .Select(x => x.Message)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["equal", "new"], remainingMessages);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNowUtc));
    }

    private static EntityErrorLog NewErrorLog(string message, DateTime occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Source = ErrorLogSources.Backend,
            Area = ErrorLogAreas.ApiRequest,
            ExceptionType = typeof(InvalidOperationException).FullName!,
            Message = message,
            TraceIdentifier = $"trace-{message}",
            RequestMethod = "GET",
            RequestPath = "/api/test"
        };

    private sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc);
    }
}
