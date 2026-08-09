using System.Text.Json;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Contracts.ErrorLogs;
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
            "request failed with bearer abc123, password=super-secret, and cookie=session-private");

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
                ContextJson: """{"authorization":"token=private-value","note":"cookie=context-private"}"""),
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
        Assert.Contains("cookie=[redacted]", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.Equal("/api/test?access_token=[redacted]", errorLog.RequestPath);
        Assert.DoesNotContain("private-value", errorLog.ContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("context-private", errorLog.ContextJson, StringComparison.Ordinal);
        using var contextDocument = JsonDocument.Parse(errorLog.ContextJson);
        Assert.False(contextDocument.RootElement.TryGetProperty("authorization", out _));
        Assert.Equal(
            "cookie=[redacted]",
            contextDocument.RootElement.GetProperty("note").GetString());
        Assert.Equal(ActorUserId, errorLog.ActorUserId);
    }

    [Fact]
    public async Task LogExceptionAsync_WithInvalidContext_ShouldPersistValidMarkerJson()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();

        // Act
        var id = await service.LogExceptionAsync(
            new InvalidOperationException("failed"),
            new ErrorLogContext(
                ErrorLogSources.Backend,
                ErrorLogAreas.ApiRequest,
                ContextJson: "not-json"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(id);
        var errorLog = await DbContextForAssert.ErrorLogs.SingleAsync(
            x => x.Id == id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(errorLog.ContextJson);
        using var contextDocument = JsonDocument.Parse(errorLog.ContextJson);
        Assert.True(contextDocument.RootElement.GetProperty("contextInvalid").GetBoolean());
    }

    [Fact]
    public async Task LogExceptionAsync_WithOversizedContext_ShouldPersistValidMarkerJson()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var values = Enumerable.Range(0, 20)
            .ToDictionary(index => $"value{index}", _ => new string('x', 3_000));

        // Act
        var id = await service.LogExceptionAsync(
            new InvalidOperationException("failed"),
            new ErrorLogContext(
                ErrorLogSources.Backend,
                ErrorLogAreas.ApiRequest,
                ContextJson: JsonSerializer.Serialize(values)),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(id);
        var errorLog = await DbContextForAssert.ErrorLogs.SingleAsync(
            x => x.Id == id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(errorLog.ContextJson);
        using var contextDocument = JsonDocument.Parse(errorLog.ContextJson);
        Assert.True(contextDocument.RootElement.GetProperty("contextTruncated").GetBoolean());
    }

    [Fact]
    public async Task ReportClientErrorAsync_ShouldPersistSanitisedFrontendDetails()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var context = JsonSerializer.Deserialize<JsonElement>(
            """{"status":"broken","accessToken":"must-not-persist","nested":{"cookie":"must-not-persist","safe":"value"},"content":"must-not-persist","note":"secret=hide-me"}""");
        var request = NewClientErrorRequest(context) with
        {
            Message = "render failed with bearer browser-secret and cookie=browser-cookie",
            StackTrace = "at render password=private-value https://boardoil.test/assets/app.js?cache=public-value:10",
            RoutePath = "/boards/7?search=music&access_token=query-secret#dialog",
            UserAgent = "Browser authorization=session-secret"
        };

        // Act
        var result = await service.ReportClientErrorAsync(
            request,
            ActorUserId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var errorLog = await DbContextForAssert.ErrorLogs.SingleAsync(
            x => x.Id == result.Data!.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(FixedNowUtc, errorLog.OccurredAtUtc);
        Assert.Equal(ErrorLogSources.Frontend, errorLog.Source);
        Assert.Equal(ErrorLogAreas.WebClient, errorLog.Area);
        Assert.Equal("TypeError", errorLog.ExceptionType);
        Assert.Contains("bearer [redacted]", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cookie=[redacted]", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser-secret", errorLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-cookie", errorLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-secret", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.Contains("https://boardoil.test/assets/app.js?cache=public-value:10", errorLog.StackTrace, StringComparison.Ordinal);
        Assert.Equal("/boards/7?search=music&access_token=[redacted]#dialog", errorLog.RequestPath);
        Assert.Equal(ActorUserId, errorLog.ActorUserId);
        Assert.NotNull(errorLog.ContextJson);
        using var contextDocument = JsonDocument.Parse(errorLog.ContextJson);
        var root = contextDocument.RootElement;
        Assert.Equal("vue", root.GetProperty("phase").GetString());
        Assert.Equal("board", root.GetProperty("routeName").GetString());
        Assert.Equal(
            "/boards/7?search=music&access_token=[redacted]#dialog",
            root.GetProperty("routePath").GetString());
        Assert.Equal("broken", root.GetProperty("clientContext").GetProperty("status").GetString());
        Assert.Equal("value", root.GetProperty("clientContext").GetProperty("nested").GetProperty("safe").GetString());
        Assert.False(root.GetProperty("clientContext").TryGetProperty("accessToken", out _));
        Assert.False(root.GetProperty("clientContext").TryGetProperty("content", out _));
        Assert.False(root.GetProperty("clientContext").GetProperty("nested").TryGetProperty("cookie", out _));
        Assert.DoesNotContain("must-not-persist", errorLog.ContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hide-me", errorLog.ContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("query-secret", errorLog.ContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("session-secret", errorLog.ContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportClientErrorAsync_WhenRequiredFieldsMissing_ShouldReturnBadRequest()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var request = NewClientErrorRequest() with { Message = " ", Phase = string.Empty };

        // Act
        var result = await service.ReportClientErrorAsync(
            request,
            ActorUserId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("message", result.ValidationErrors.Keys);
        Assert.Contains("phase", result.ValidationErrors.Keys);
        Assert.Empty(DbContextForAssert.ErrorLogs);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"not-an-object\"")]
    public async Task ReportClientErrorAsync_WhenContextIsNotAnObject_ShouldReturnBadRequest(
        string contextJson)
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var context = JsonSerializer.Deserialize<JsonElement>(contextJson);

        // Act
        var result = await service.ReportClientErrorAsync(
            NewClientErrorRequest(context),
            ActorUserId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("context", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task ReportClientErrorAsync_WhenViewportIsInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var request = NewClientErrorRequest() with
        {
            Viewport = new ClientErrorViewportDto(-1, 720)
        };

        // Act
        var result = await service.ReportClientErrorAsync(
            request,
            ActorUserId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("viewport", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task ReportClientErrorAsync_WhenContextIsTooLarge_ShouldReturnBadRequest()
    {
        // Arrange
        var service = ResolveService<IErrorLogService>();
        var oversizedValue = new string('x', 17_000);
        var context = JsonSerializer.SerializeToElement(new { value = oversizedValue });

        // Act
        var result = await service.ReportClientErrorAsync(
            NewClientErrorRequest(context),
            ActorUserId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("context", result.ValidationErrors.Keys);
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

    private static ClientErrorReportRequest NewClientErrorRequest(JsonElement? context = null) =>
        new(
            "render failed",
            "TypeError",
            "at render",
            "vue",
            "board",
            "/boards/7",
            "1.4.0 (dev/local) abc123",
            new ClientErrorViewportDto(1280, 720),
            "Test Browser",
            context);

    private sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc);
    }
}
