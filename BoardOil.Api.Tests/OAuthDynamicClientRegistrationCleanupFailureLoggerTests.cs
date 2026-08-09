using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class OAuthDynamicClientRegistrationCleanupFailureLoggerTests
{
    [Fact]
    public async Task LogAsync_ShouldPersistBackgroundServiceFailure()
    {
        // Arrange
        var errorLogService = new RecordingErrorLogService();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IErrorLogService>(errorLogService)
            .BuildServiceProvider();
        var failureLogger = new OAuthDynamicClientRegistrationCleanupFailureLogger(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OAuthDynamicClientRegistrationCleanupFailureLogger>.Instance);
        var exception = new InvalidOperationException("cleanup failed");

        // Act
        await failureLogger.LogAsync(exception);

        // Assert
        Assert.Same(exception, errorLogService.Exception);
        Assert.NotNull(errorLogService.Context);
        Assert.Equal(ErrorLogSources.Backend, errorLogService.Context!.Source);
        Assert.Equal(ErrorLogAreas.BackgroundService, errorLogService.Context.Area);
        Assert.Contains(
            nameof(OAuthDynamicClientRegistrationCleanupService),
            errorLogService.Context.ContextJson,
            StringComparison.Ordinal);
    }

    private sealed class RecordingErrorLogService : IErrorLogService
    {
        public Exception? Exception { get; private set; }

        public ErrorLogContext? Context { get; private set; }

        public Task<ApiResult<ErrorLogListDto>> ListAsync(int? offset, int? limit) =>
            throw new NotSupportedException();

        public Task<ApiResult<ErrorLogDetailsDto>> GetAsync(int id) =>
            throw new NotSupportedException();

        public Task<ApiResult<ErrorLogPurgeResultDto>> PurgeExpiredAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int?> LogExceptionAsync(
            Exception exception,
            ErrorLogContext context,
            CancellationToken cancellationToken = default)
        {
            Exception = exception;
            Context = context;
            return Task.FromResult<int?>(17);
        }
    }
}
