using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Ef.Diagnostics;
using BoardOil.Services.ErrorLogs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SqliteLockDiagnosticsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WalWriteContention_ShouldCaptureBothContextPathsAndPersistEvidenceAfterBlockerDisposes(bool ambient)
    {
        // Arrange
        await using var database = await Database.CreateAsync("WAL");
        var scopes = database.Provider.GetRequiredService<IDbContextScopeFactory>();
        var blockerName = DatabaseDiagnosticOperation.Begin("job:test-blocker:42");
        var blocker = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var holder = blocker.DbContexts.Get<BoardOilDbContext>();
        _ = await holder.AppSettings.CountAsync();
        var holderId = holder.ContextId.InstanceId;

        // Act
        DbUpdateException exception;
        try
        {
            exception = await FailedWriteAsync(database, ambient);
        }
        finally
        {
            blocker.Dispose();
            blockerName.Dispose();
        }
        var service = new ErrorLogService(scopes,
            database.Provider.GetRequiredService<BoardOil.Data.Abstractions.ErrorLogs.IErrorLogRepository>(),
            TimeProvider.System, NullLogger<ErrorLogService>.Instance);
        var id = await service.LogExceptionAsync(new AggregateException("evaluation failed", exception),
            new ErrorLogContext("Backend", "JobScheduler", ContextJson: "{\"service\":\"JobSchedulerService\"}"));
        var stored = await service.GetAsync(id!.Value);

        // Assert
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal(5, failure.SqliteErrorCode);
        Assert.Equal(5, failure.SqliteExtendedErrorCode);
        Assert.Equal("wal", failure.JournalModeAtStartup);
        Assert.True(failure.ElapsedMilliseconds > 0);
        Assert.Equal("UPDATE", failure.FailedOperation!.CommandKind);
        Assert.Equal("schedule:oauth-client-registration-purge", failure.FailedOperation.Operation);
        Assert.Contains(failure.ActiveTransactions, transaction => transaction.ContextId == holderId
            && transaction.Operation == "job:test-blocker:42");
        Assert.True(stored.Success);
        using var context = JsonDocument.Parse(stored.Data!.ContextJson!);
        Assert.Equal("JobSchedulerService", context.RootElement.GetProperty("service").GetString());
        var captured = context.RootElement.GetProperty("databaseLocks")[0];
        Assert.Equal(5, captured.GetProperty("SqliteErrorCode").GetInt32());
        Assert.Equal("job:test-blocker:42", captured.GetProperty("ActiveTransactions")[0].GetProperty("Operation").GetString());
        var output = stored.Data.ContextJson + string.Join('\n', database.Logger.Messages);
        Assert.DoesNotContain(Database.Secret, output);
        Assert.DoesNotContain(database.ConnectionString, output);
        Assert.DoesNotContain("UPDATE \"", output);
        Assert.Contains(database.Logger.Messages, message => message.Contains("journal mode at startup: wal"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitTransactionStartFailure_ShouldCaptureTimingAndBlockerBeforeScopeUnwinds(bool asynchronous)
    {
        // Arrange
        await using var database = await Database.CreateAsync("WAL");
        await using var ownerScope = database.Provider.CreateAsyncScope();
        var owner = ownerScope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        using var ownerOperation = DatabaseDiagnosticOperation.Begin("job:test-blocker:42");
        await using var ownerTransaction = await owner.Database.BeginTransactionAsync();
        var scopes = database.Provider.GetRequiredService<IDbContextScopeFactory>();
        using var operation = DatabaseDiagnosticOperation.Begin("http:POST /test-begin");

        // Act
        SqliteException exception;
        if (asynchronous)
        {
            using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
            exception = await Assert.ThrowsAsync<SqliteException>(() => scope.Transaction((_, _) => Task.CompletedTask));
        }
        else
        {
            using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
            exception = Assert.Throws<SqliteException>(() => scope.DbContexts.Get<BoardOilDbContext>());
        }

        // Assert
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal("BeginTransaction", failure.Phase);
        Assert.Equal(5, failure.SqliteErrorCode);
        Assert.Equal(exception.SqliteExtendedErrorCode, failure.SqliteExtendedErrorCode);
        Assert.Equal("wal", failure.JournalModeAtStartup);
        Assert.True(failure.ElapsedMilliseconds > 0);
        Assert.NotNull(failure.ContextId);
        Assert.Null(failure.FailedOperation);
        Assert.Equal("http:POST /test-begin", failure.Operation);
        Assert.Contains(failure.ActiveTransactions, active => active.ContextId == owner.ContextId.InstanceId
            && active.Operation == "job:test-blocker:42");
    }

    [Fact]
    public async Task UnobservedDirectBeginFailure_ShouldGainFallbackCodesWhenLoggedWithoutInventingTiming()
    {
        // Arrange
        await using var database = await Database.CreateAsync("WAL");
        await using var external = new SqliteConnection(database.ConnectionString);
        await external.OpenAsync();
        await using var blocker = await external.BeginTransactionAsync();
        await using var writerScope = database.Provider.CreateAsyncScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        var exception = await Assert.ThrowsAsync<SqliteException>(() => writer.Database.BeginTransactionAsync());
        Assert.Empty(DatabaseLockFailure.FromException(exception));
        await blocker.RollbackAsync();
        var service = new ErrorLogService(database.Provider.GetRequiredService<IDbContextScopeFactory>(),
            database.Provider.GetRequiredService<BoardOil.Data.Abstractions.ErrorLogs.IErrorLogRepository>(),
            TimeProvider.System, NullLogger<ErrorLogService>.Instance,
            database.Provider.GetRequiredService<IDatabaseFailureDiagnostics>());

        // Act
        var id = await service.LogExceptionAsync(new AggregateException("outer", exception),
            new ErrorLogContext("Backend", "ApiRequest"));
        var stored = await service.GetAsync(id!.Value);

        // Assert
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal("ErrorLogFallback", failure.Phase);
        Assert.Equal(exception.SqliteExtendedErrorCode, failure.SqliteExtendedErrorCode);
        Assert.Equal("wal", failure.JournalModeAtStartup);
        Assert.Null(failure.ElapsedMilliseconds);
        Assert.Null(failure.ContextId);
        Assert.Null(failure.FailedOperation);
        using var context = JsonDocument.Parse(stored.Data!.ContextJson!);
        Assert.Equal(5, context.RootElement.GetProperty("databaseLocks")[0].GetProperty("SqliteErrorCode").GetInt32());
    }

    [Fact]
    public async Task ReaderFinalisationFailure_ShouldCaptureTheProductionStackAndOtherActiveReader()
    {
        // Arrange
        await using var database = await Database.CreateAsync("DELETE");
        await using var scope = database.Provider.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        using var operation = DatabaseDiagnosticOperation.Begin("http:GET /test-reader");
        await using var reader = holder.AppSettings.AsAsyncEnumerable().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());

        // Act
        var exception = await FailedWriteAsync(database, true);

        // Assert
        Assert.Contains("SqliteDataRecord.Dispose", exception.ToString());
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal("SaveChanges", failure.Phase);
        Assert.Equal("delete", failure.JournalModeAtStartup);
        Assert.Contains(failure.ActiveOperations, command => command.ContextId == holder.ContextId.InstanceId
            && command.CommandKind == "SELECT" && command.Operation == "http:GET /test-reader");
    }

    [Fact]
    public async Task TransactionCommitFailure_ShouldCaptureTransactionDurationAndExtendedCode()
    {
        // Arrange
        await using var database = await Database.CreateAsync("DELETE");
        await using var readerScope = database.Provider.CreateAsyncScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        await using var reader = readerContext.AppSettings.AsAsyncEnumerable().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        await using var writerScope = database.Provider.CreateAsyncScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        using var operation = DatabaseDiagnosticOperation.Begin("http:POST /test-commit");
        await using var transaction = await writer.Database.BeginTransactionAsync();
        (await writer.AppSettings.FirstAsync()).Value = "changed";
        await writer.SaveChangesAsync();

        // Act
        var exception = await Assert.ThrowsAsync<SqliteException>(() => transaction.CommitAsync());

        // Assert
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal("Transaction", failure.Phase);
        Assert.Equal(exception.SqliteExtendedErrorCode, failure.SqliteExtendedErrorCode);
        Assert.True(failure.ElapsedMilliseconds > 0);
        Assert.Contains(failure.ActiveTransactions, active => active.TransactionId == transaction.TransactionId
            && active.Operation == "http:POST /test-commit");
    }

    [Fact]
    public async Task DisposedTransaction_ShouldNotBeReportedAsAnActiveBlockerOnLaterFailure()
    {
        // Arrange
        await using var database = await Database.CreateAsync("WAL");
        await using (var scope = database.Provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
        }
        // An uninstrumented connection represents work outside the observed EF contexts.
        await using var external = new SqliteConnection(database.ConnectionString);
        await external.OpenAsync();
        await using var blocker = await external.BeginTransactionAsync();

        // Act
        var exception = await FailedWriteAsync(database, false);

        // Assert
        var failure = Assert.Single(DatabaseLockFailure.FromException(exception));
        Assert.Equal(0, failure.ActiveTransactionCount);
        Assert.Empty(failure.ActiveTransactions);
    }

    private static async Task<DbUpdateException> FailedWriteAsync(Database database, bool ambient)
    {
        using var operation = DatabaseDiagnosticOperation.Begin("schedule:oauth-client-registration-purge");
        if (ambient)
        {
            using var scope = database.Provider.GetRequiredService<IDbContextScopeFactory>().Create(DbContextScopeOption.ForceCreateNew);
            var context = scope.DbContexts.Get<BoardOilDbContext>();
            (await context.ScheduledJobSchedulerStates.SingleAsync()).LastEvaluatedAtUtc = DateTime.UtcNow;
            return await Assert.ThrowsAsync<DbUpdateException>(() => scope.SaveChangesAsync());
        }
        await using var services = database.Provider.CreateAsyncScope();
        var db = services.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        (await db.ScheduledJobSchedulerStates.SingleAsync()).LastEvaluatedAtUtc = DateTime.UtcNow;
        return await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private sealed class Database : IAsyncDisposable
    {
        public const string Secret = "bo_pat_should_never_appear_in_database_diagnostics";
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"boardoil-lock-diagnostics-{Guid.NewGuid():N}");
        public string ConnectionString { get; }
        public ServiceProvider Provider { get; }
        public RecordingLogger Logger { get; } = new();

        private Database()
        {
            Directory.CreateDirectory(directory);
            ConnectionString = $"Data Source={Path.Combine(directory, "test.db")};Default Timeout=1;Pooling=False";
            Provider = new ServiceCollection().AddBoardOilEfInfrastructure(ConnectionString)
                .AddSingleton<ILogger<SqliteLockDiagnostics>>(Logger).BuildServiceProvider();
        }

        public static async Task<Database> CreateAsync(string mode)
        {
            var database = new Database();
            await using var context = database.Provider.GetRequiredService<IDbContextFactory>()
                .CreateDbContext<BoardOilDbContext>();
            await context.Database.EnsureCreatedAsync();
            context.AppSettings.AddRange(new EntityAppSetting { Key = "first", Value = Secret },
                new EntityAppSetting { Key = "second", Value = "another value" });
            context.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
            {
                Name = "oauth-client-registration-purge-checkpoint", LastRunTimeUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            await context.Database.OpenConnectionAsync();
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = mode == "WAL" ? "PRAGMA journal_mode=WAL;" : "PRAGMA journal_mode=DELETE;";
            await command.ExecuteScalarAsync();
            await database.Provider.GetRequiredService<SqliteLockDiagnostics>().ReportJournalModeAsync(context);
            return database;
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingLogger : ILogger<SqliteLockDiagnostics>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
