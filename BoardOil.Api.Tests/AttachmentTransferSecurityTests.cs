using BoardOil.Api.Configuration;
using BoardOil.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AttachmentTransferSecurityTests
{
    [Fact]
    public void McpLogging_ShouldSuppressSensitiveTraceDespiteCategoryAndProviderOverrides()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestLogProvider());
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddFilter<TestLogProvider>("ModelContextProtocol.Server", LogLevel.Trace);
        });
        services.AddBoardOilMcp(new BoardOilMcpOptions());
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        var sdk = factory.CreateLogger("ModelContextProtocol.Server.StreamableHttpPostTransport");
        var other = factory.CreateLogger("BoardOil.Tests");

        Assert.False(sdk.IsEnabled(LogLevel.Trace));
        Assert.True(sdk.IsEnabled(LogLevel.Debug));
        Assert.True(other.IsEnabled(LogLevel.Trace));
    }

    private sealed class TestLogProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger();
        public void Dispose() { }
    }
    private sealed class TestLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
