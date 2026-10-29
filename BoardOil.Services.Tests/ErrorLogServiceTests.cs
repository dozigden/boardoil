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

    [Theory]
    [InlineData("Échec du rendu", "échec", true)]
    [InlineData("échec du rendu", "ÉCHEC", true)]
    [InlineData("CAFÉ unavailable", "café", true)]
    [InlineData("Queue [Échec].* 50%_complete", "[échec].* 50%_", true)]
    [InlineData("Queue [error]", "[", true)]
    [InlineData("café unavailable", "cafe", false)]
    public async Task SearchAsync_ShouldMatchUnicodeCasePairsAndTreatSearchAsLiteral(string message, string search, bool expectedMatch)
    {
        DbContextForArrange.ErrorLogs.AddRange(
            NewErrorLog(message, FixedNowUtc),
            NewErrorLog("unrelated error", FixedNowUtc),
            NewErrorLog("Queue éZ 50%_complete", FixedNowUtc));
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        var result = await service.SearchAsync(new ErrorLogSearchRequest(Message: search, Limit: 1));

        Assert.True(result.Success);
        Assert.Equal(expectedMatch ? 1 : 0, result.Data!.TotalCount);
        if (expectedMatch)
        {
            Assert.Equal(message, Assert.Single(result.Data.Items).Message);
        }
        else
        {
            Assert.Empty(result.Data.Items);
        }
    }

    [Theory]
    [InlineData("Cookie: theme=dark; boardoil_refresh=private-cookie; session=private-cookie")]
    [InlineData("cOoKiE\t:\ttheme=dark; custom_session=private-cookie")]
    [InlineData("Set-Cookie: session=private-cookie; Path=/; HttpOnly")]
    [InlineData("\"Cookie\": \"theme=dark; custom_session=private-cookie\"")]
    public async Task Reads_ShouldRedactCompleteCookieHeadersFromOlderRecords(string header)
    {
        var entity = NewErrorLog($"Request failed: {header}", FixedNowUtc);
        entity.StackTrace = $"{header}\r\nat SafeComponent";
        entity.ContextJson = JsonSerializer.Serialize(new { nested = new { headers = $"{header}\nresponse failed" }, safe = "diagnostic" });
        DbContextForArrange.ErrorLogs.Add(entity);
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        var list = await service.SearchAsync(new ErrorLogSearchRequest());
        var detail = await service.GetAsync(entity.Id);

        Assert.True(list.Success);
        Assert.True(detail.Success);
        Assert.DoesNotContain("private-cookie", JsonSerializer.Serialize(list.Data));
        Assert.DoesNotContain("private-cookie", JsonSerializer.Serialize(detail.Data));
        Assert.Contains("[redacted]\r\nat SafeComponent", detail.Data!.StackTrace!);
        using var context = JsonDocument.Parse(detail.Data.ContextJson!);
        Assert.Contains("[redacted]\nresponse failed", context.RootElement.GetProperty("nested").GetProperty("headers").GetString()!);
        Assert.Equal("diagnostic", context.RootElement.GetProperty("safe").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_ShouldRedactCompleteCookieHeadersBeforePersistence(bool frontend)
    {
        const string header = "Cookie: theme=dark; boardoil_refresh=private-cookie; custom_session=private-cookie";
        var service = ResolveService<IErrorLogService>();

        if (frontend)
        {
            var result = await service.ReportClientErrorAsync(NewClientErrorRequest() with
            {
                Message = header,
                StackTrace = $"{header}\nat SafeComponent",
                Context = JsonSerializer.SerializeToElement(new { headers = header })
            }, ActorUserId, TestContext.Current.CancellationToken);
            Assert.True(result.Success);
        }
        else
        {
            var id = await service.LogExceptionAsync(new InvalidOperationException($"{header}\nat SafeComponent"),
                new ErrorLogContext(ErrorLogSources.Backend, ErrorLogAreas.ApiRequest,
                    ContextJson: JsonSerializer.Serialize(new { headers = header })), TestContext.Current.CancellationToken);
            Assert.NotNull(id);
        }

        var stored = await DbContextForAssert.ErrorLogs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("private-cookie", stored.Message);
        Assert.DoesNotContain("private-cookie", stored.StackTrace!);
        Assert.DoesNotContain("private-cookie", stored.ContextJson!);
        Assert.Contains("[redacted]\nat SafeComponent", stored.StackTrace!);
    }

    [Fact]
    public async Task SearchAsync_ShouldCombineFiltersAndUseStablePagination()
    {
        var older = NewErrorLog("Failure 50%_complete", FixedNowUtc.AddMinutes(-1));
        var first = NewErrorLog("FAILURE 50%_complete", FixedNowUtc);
        var second = NewErrorLog("Failure 50%_complete", FixedNowUtc);
        var excluded = NewErrorLog("Failure 50%_complete", FixedNowUtc);
        excluded.Area = "other";
        var wrongSource = NewErrorLog("Failure 50%_complete", FixedNowUtc);
        wrongSource.Source = "Frontend";
        var errors = new[] { older, first, second, excluded, wrongSource,
            NewErrorLog("Failure 50x_complete", FixedNowUtc),
            NewErrorLog("Failure 50%_complete", FixedNowUtc.AddMinutes(1)) };
        foreach (var error in errors) error.TraceIdentifier = "shared-trace";
        var wrongTrace = NewErrorLog("Failure 50%_complete", FixedNowUtc);
        DbContextForArrange.ErrorLogs.AddRange(errors.Append(wrongTrace));
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        var result = await service.SearchAsync(new ErrorLogSearchRequest(
            Offset: 1, Limit: 1, FromUtc: new DateTimeOffset(FixedNowUtc),
            ToUtc: new DateTimeOffset(FixedNowUtc.AddMinutes(1)),
            Source: first.Source, Area: first.Area, Message: "failure 50%_", TraceIdentifier: first.TraceIdentifier));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(first.Id, Assert.Single(result.Data.Items).Id);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("area")]
    [InlineData("message")]
    [InlineData("traceIdentifier")]
    [InlineData("toUtc")]
    public async Task SearchAsync_ShouldRejectInvalidFilters(string field)
    {
        var request = field switch
        {
            "source" => new ErrorLogSearchRequest(Source: new string('x', 33)),
            "area" => new ErrorLogSearchRequest(Area: new string('x', 65)),
            "message" => new ErrorLogSearchRequest(Message: new string('x', 2049)),
            "traceIdentifier" => new ErrorLogSearchRequest(TraceIdentifier: new string('x', 129)),
            _ => new ErrorLogSearchRequest(FromUtc: new DateTimeOffset(FixedNowUtc), ToUtc: new DateTimeOffset(FixedNowUtc))
        };

        var result = await ResolveService<IErrorLogService>().SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(field, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Reads_ShouldSanitisePreviouslyStoredCredentialsAndNestedContext()
    {
        var entity = NewErrorLog("failed refresh_token=private-refresh api_key=private-key bo_pat_private", FixedNowUtc);
        entity.StackTrace = "Bearer private-bearer password=\"private password\" eyJheader.payload.signature";
        entity.ContextJson = """{"nested":{"client_secret":"private-secret","apiKey":"private-key","reason":"Bearer private-context"},"safe":"diagnostic","clientContext":{"render":{"component":"Board","password":"private-nested"}}}""";
        DbContextForArrange.ErrorLogs.Add(entity);
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IErrorLogService>();

        var list = await service.SearchAsync(new ErrorLogSearchRequest());
        var detail = await service.GetAsync(entity.Id);

        Assert.DoesNotContain("private", JsonSerializer.Serialize(list.Data));
        Assert.DoesNotContain("private", JsonSerializer.Serialize(detail.Data));
        Assert.DoesNotContain("eyJheader", detail.Data!.StackTrace!);
        Assert.Contains("diagnostic", detail.Data.ContextJson!);
        Assert.Contains("[redacted]", detail.Data.StackTrace!);
        Assert.DoesNotContain("client_secret", detail.Data.ContextJson!);
        Assert.DoesNotContain("apiKey", detail.Data.ContextJson!);
        using var context = JsonDocument.Parse(detail.Data.ContextJson!);
        Assert.Equal("Board", context.RootElement.GetProperty("clientContext").GetProperty("render").GetProperty("component").GetString());
    }

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
        Assert.NotNull(errorLog.ContextJson);
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
