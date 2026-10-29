using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BoardOil.Abstractions.DataAccess;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BoardOil.Ef.Diagnostics;

public sealed class SqliteLockDiagnostics : IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>, IDatabaseFailureDiagnostics, IDisposable
{
    private static readonly HashSet<string> EventNames =
    [
        CoreEventId.SaveChangesStarting.Name!, CoreEventId.SaveChangesFailed.Name!, CoreEventId.ContextDisposed.Name!,
        RelationalEventId.CommandExecuting.Name!, RelationalEventId.CommandExecuted.Name!,
        RelationalEventId.CommandError.Name!, RelationalEventId.CommandCanceled.Name!,
        RelationalEventId.DataReaderDisposing.Name!, RelationalEventId.TransactionStarted.Name!,
        RelationalEventId.TransactionUsed.Name!, RelationalEventId.TransactionCommitted.Name!,
        RelationalEventId.TransactionRolledBack.Name!, RelationalEventId.TransactionDisposed.Name!,
        RelationalEventId.TransactionError.Name!
    ];
    private static readonly string[] CommandKinds = ["SELECT", "INSERT", "UPDATE", "DELETE", "PRAGMA"];
    private readonly ConditionalWeakTable<BoardOilDbContext, ContextState> contexts = new();
    private readonly ConcurrentDictionary<Guid, CommandState> commands = new();
    private readonly ConcurrentDictionary<Guid, TransactionState> transactions = new();
    private readonly ConcurrentDictionary<string, string> journalModes = new(StringComparer.Ordinal);
    private readonly List<IDisposable> subscriptions = [];
    private readonly ILogger<SqliteLockDiagnostics> logger;
    private readonly IDisposable listenerSubscription;
    private readonly string? configuredDatabase;
    private int collectionWarningLogged;
    private bool disposed;

    public SqliteLockDiagnostics(ILogger<SqliteLockDiagnostics> logger, string? connectionString = null)
    {
        this.logger = logger;
        if (connectionString is not null)
        {
            configuredDatabase = NormaliseDatabaseKey(new SqliteConnectionStringBuilder(connectionString).DataSource);
        }
        listenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public void TrackContext(BoardOilDbContext context) => contexts.Add(context, new ContextState());

    public void Enrich(Exception exception)
    {
        try
        {
            foreach (var sqlite in FindSqliteExceptions(exception)
                .Where(x => x.SqliteErrorCode is 5 or 6).Take(2))
            {
                if (DatabaseLockFailure.FromException(sqlite).Count > 0) { continue; }
                CaptureSnapshot(sqlite, configuredDatabase, null, null, "ErrorLogFallback", null);
            }
        }
        catch (Exception) { LogCollectionFailure(); }
    }

    public void CaptureTransactionStartFailure(BoardOilDbContext context, Exception exception, long started)
    {
        try
        {
            if (contexts.TryGetValue(context, out var state))
            {
                // The most recent query belongs to preceding work, not this failed BEGIN.
                Capture(context, state, exception, "BeginTransaction", started, includeLastCommand: false);
            }
        }
        catch (Exception) { LogCollectionFailure(); }
    }

    public async Task ReportJournalModeAsync(BoardOilDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        try
        {
            if (opened) { await connection.OpenAsync(); }
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 1;
            command.CommandText = "PRAGMA main.journal_mode;";
            if (await command.ExecuteScalarAsync() is string mode)
            {
                journalModes[DatabaseKey(context)] = mode;
                logger.LogInformation("SQLite journal mode at startup: {JournalMode}.", mode);
            }
        }
        catch (Exception)
        {
            logger.LogWarning("Could not read SQLite journal mode for startup diagnostics.");
        }
        finally
        {
            if (opened)
            {
                try { await connection.CloseAsync(); }
                catch (Exception) { logger.LogWarning("Could not close the SQLite startup diagnostics connection."); }
            }
        }
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name != DbLoggerCategory.Name) { return; }
        lock (subscriptions)
        {
            if (!disposed)
            {
                subscriptions.Add(listener.Subscribe(this, name => EventNames.Contains(name)));
            }
        }
    }

    public void OnNext(KeyValuePair<string, object?> notification)
    {
        // Diagnostic observers must never alter the database operation's outcome.
        try
        {
            Observe(notification.Key, notification.Value);
        }
        catch (Exception)
        {
            LogCollectionFailure();
        }
    }

    private void Observe(string name, object? value)
    {
        if (value is not DbContextEventData { Context: BoardOilDbContext context }
            || !contexts.TryGetValue(context, out var state)) { return; }
        var contextId = context.ContextId.InstanceId;
        if (name == CoreEventId.ContextDisposed.Name)
        {
            foreach (var item in commands.Where(x => x.Value.ContextId == contextId))
            {
                commands.TryRemove(item.Key, out _);
            }
            foreach (var item in transactions.Where(x => x.Value.ContextId == contextId))
            {
                transactions.TryRemove(item.Key, out _);
            }
            contexts.Remove(context);
            return;
        }
        if (name == CoreEventId.SaveChangesStarting.Name)
        {
            state.SaveStarted = Stopwatch.GetTimestamp();
            state.LastCommand = null;
        }
        else if (name == CoreEventId.SaveChangesFailed.Name && value is DbContextErrorEventData saveError)
        {
            Capture(context, state, saveError.Exception, "SaveChanges", state.SaveStarted);
        }
        else if (name == RelationalEventId.CommandExecuting.Name && value is CommandEventData command)
        {
            var entry = new CommandState(state.Database ??= DatabaseKey(context), contextId, command.ConnectionId,
                OperationName(), Activity.Current?.TraceId.ToString(), CommandKind(command.Command.CommandText),
                Stopwatch.GetTimestamp());
            state.LastCommand = entry;
            commands[command.CommandId] = entry;
        }
        else if (name == RelationalEventId.CommandError.Name && value is CommandErrorEventData commandError)
        {
            Capture(context, state, commandError.Exception, "Command", state.LastCommand?.Started ?? 0);
            commands.TryRemove(commandError.CommandId, out _);
        }
        else if (name == RelationalEventId.CommandExecuted.Name && value is CommandExecutedEventData executed)
        {
            if (executed.ExecuteMethod != DbCommandMethod.ExecuteReader)
            {
                commands.TryRemove(executed.CommandId, out _);
            }
        }
        else if (name == RelationalEventId.DataReaderDisposing.Name && value is DataReaderDisposingEventData reader)
        {
            commands.TryRemove(reader.CommandId, out _);
        }
        else if (name == RelationalEventId.CommandCanceled.Name && value is CommandEndEventData canceled)
        {
            commands.TryRemove(canceled.CommandId, out _);
        }
        else if ((name == RelationalEventId.TransactionStarted.Name || name == RelationalEventId.TransactionUsed.Name)
            && value is TransactionEventData transaction)
        {
            transactions[transaction.TransactionId] = new TransactionState(state.Database ??= DatabaseKey(context),
                transaction.TransactionId, contextId, transaction.ConnectionId, OperationName(),
                Activity.Current?.TraceId.ToString(), transaction.Transaction.IsolationLevel.ToString(),
                Stopwatch.GetTimestamp());
        }
        else if (name == RelationalEventId.TransactionError.Name && value is TransactionErrorEventData transactionError)
        {
            transactions.TryGetValue(transactionError.TransactionId, out var active);
            Capture(context, state, transactionError.Exception, "Transaction", active?.Started ?? 0);
        }
        else if ((name == RelationalEventId.TransactionCommitted.Name
                || name == RelationalEventId.TransactionRolledBack.Name
                || name == RelationalEventId.TransactionDisposed.Name)
            && value is TransactionEventData ended)
        {
            transactions.TryRemove(ended.TransactionId, out _);
        }
    }

    private void Capture(BoardOilDbContext context, ContextState state, Exception exception, string phase, long started,
        bool includeLastCommand = true)
    {
        var sqlite = FindSqliteExceptions(exception).FirstOrDefault(x => x.SqliteErrorCode is 5 or 6);
        if (sqlite is null || sqlite.SqliteErrorCode is not (5 or 6)
            || DatabaseLockFailure.FromException(sqlite).Count > 0) { return; }
        CaptureSnapshot(sqlite, state.Database ??= DatabaseKey(context), context.ContextId.InstanceId,
            includeLastCommand ? state.LastCommand : null, phase, Elapsed(started));
    }

    private void CaptureSnapshot(SqliteException sqlite, string? key, Guid? contextId, CommandState? failedCommand,
        string phase, double? elapsed)
    {
        var activeCommands = commands.Values.Where(x => x.Database == key).OrderBy(x => x.Started).ToArray();
        var activeTransactions = transactions.Values.Where(x => x.Database == key).OrderBy(x => x.Started).ToArray();
        string? mode = null;
        if (key is not null) { journalModes.TryGetValue(key, out mode); }
        var failure = new DatabaseLockFailure(DateTime.UtcNow, sqlite.SqliteErrorCode, sqlite.SqliteExtendedErrorCode,
            phase, elapsed, mode, contextId, OperationName(), failedCommand?.Snapshot(),
            activeCommands.Length, activeCommands.Take(4).Select(x => x.Snapshot()).ToArray(),
            activeTransactions.Length, activeTransactions.Take(4).Select(x => x.Snapshot()).ToArray());
        failure.AttachTo(sqlite);
        // Also retain evidence in container logs if persisting the ErrorLog hits the same lock.
        logger.LogWarning("SQLite lock failure diagnostics: {DatabaseLockFailure}",
            System.Text.Json.JsonSerializer.Serialize(failure));
    }

    private static IEnumerable<SqliteException> FindSqliteExceptions(Exception exception)
    {
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(exception);
        while (pending.Count > 0 && visited.Count < 64)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) { continue; }
            if (current is SqliteException sqlite) { yield return sqlite; }
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions) { pending.Push(inner); }
            }
            else if (current.InnerException is { } inner) { pending.Push(inner); }
        }
    }

    private static string DatabaseKey(BoardOilDbContext context)
    {
        return NormaliseDatabaseKey(context.Database.GetDbConnection().DataSource);
    }

    private static string NormaliseDatabaseKey(string source) =>
        string.IsNullOrEmpty(source) || source == ":memory:" ? source : Path.GetFullPath(source);

    private static string? OperationName()
    {
        var value = DatabaseDiagnosticOperation.Current;
        if (value is null) { return null; }
        return value.Length <= 240 ? value : value[..240];
    }

    private static string CommandKind(string sql)
    {
        var first = sql.AsSpan().TrimStart();
        var length = first.IndexOfAny(' ', '\r', '\n');
        if (length >= 0) { first = first[..length]; }
        foreach (var kind in CommandKinds)
        {
            if (first.Equals(kind, StringComparison.OrdinalIgnoreCase)) { return kind; }
        }
        return "Other";
    }

    private void LogCollectionFailure()
    {
        if (Interlocked.Exchange(ref collectionWarningLogged, 1) != 0) { return; }
        try { logger.LogWarning("Could not collect SQLite lock diagnostics. Further collection warnings are suppressed."); }
        catch (Exception) { /* Diagnostic logging must preserve the original database outcome. */ }
    }

    private static double Elapsed(long started) => started == 0 ? 0 : Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2);

    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void Dispose()
    {
        listenerSubscription.Dispose();
        lock (subscriptions)
        {
            disposed = true;
            foreach (var subscription in subscriptions) { subscription.Dispose(); }
            subscriptions.Clear();
        }
        commands.Clear();
        transactions.Clear();
    }

    private sealed class ContextState
    {
        public long SaveStarted { get; set; }
        public CommandState? LastCommand { get; set; }
        public string? Database { get; set; }
    }

    private sealed record CommandState(string Database, Guid ContextId, Guid ConnectionId, string? Operation,
        string? TraceId, string Kind, long Started)
    {
        public DatabaseOperationSnapshot Snapshot() => new(ContextId, ConnectionId, Operation,
            TraceId, Kind, Elapsed(Started));
    }

    private sealed record TransactionState(string Database, Guid TransactionId, Guid ContextId,
        Guid ConnectionId, string? Operation, string? TraceId, string IsolationLevel, long Started)
    {
        public DatabaseTransactionSnapshot Snapshot() => new(TransactionId, ContextId, ConnectionId,
            Operation, TraceId, IsolationLevel, Elapsed(Started));
    }
}
