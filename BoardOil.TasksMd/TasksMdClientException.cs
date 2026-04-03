namespace BoardOil.TasksMd;

public sealed record TasksMdClientValidationError(string Property, string Message);

public sealed class TasksMdClientException : Exception
{
    public TasksMdClientException(string message)
        : this(message, Array.Empty<TasksMdClientValidationError>())
    {
    }

    public TasksMdClientException(string message, IReadOnlyList<TasksMdClientValidationError> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public IReadOnlyList<TasksMdClientValidationError> ValidationErrors { get; }
}
