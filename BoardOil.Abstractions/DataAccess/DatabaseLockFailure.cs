namespace BoardOil.Abstractions.DataAccess;

public sealed record DatabaseLockFailure(
    DateTime CapturedAtUtc,
    int SqliteErrorCode,
    int SqliteExtendedErrorCode,
    string Phase,
    double? ElapsedMilliseconds,
    string? JournalModeAtStartup,
    Guid? ContextId,
    string? Operation,
    DatabaseOperationSnapshot? FailedOperation,
    int ActiveOperationCount,
    IReadOnlyList<DatabaseOperationSnapshot> ActiveOperations,
    int ActiveTransactionCount,
    IReadOnlyList<DatabaseTransactionSnapshot> ActiveTransactions)
{
    private const string ExceptionDataKey = "BoardOil.DatabaseLockFailure";
    public string ObservationScope => "BoardOil EF contexts in this process";

    public void AttachTo(Exception exception) => exception.Data[ExceptionDataKey] = this;

    public static IReadOnlyList<DatabaseLockFailure> FromException(Exception exception)
    {
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var failures = new List<DatabaseLockFailure>();
        pending.Push(exception);
        while (pending.Count > 0 && visited.Count < 64 && failures.Count < 2)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) { continue; }
            if (current.Data[ExceptionDataKey] is DatabaseLockFailure failure)
            {
                failures.Add(failure);
            }
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions) { pending.Push(inner); }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push(inner);
            }
        }
        return failures;
    }
}

public sealed record DatabaseOperationSnapshot(
    Guid ContextId,
    Guid ConnectionId,
    string? Operation,
    string? TraceId,
    string CommandKind,
    double ElapsedMilliseconds);

public sealed record DatabaseTransactionSnapshot(
    Guid TransactionId,
    Guid ContextId,
    Guid ConnectionId,
    string? Operation,
    string? TraceId,
    string IsolationLevel,
    double ElapsedMilliseconds);
