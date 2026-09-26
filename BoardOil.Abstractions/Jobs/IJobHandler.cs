using System.Text.Json;

namespace BoardOil.Abstractions.Jobs;

public interface IJobHandler
{
    string Type { get; }
    Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken);
}

public abstract class JobHandlerBase<TPayload> : IJobHandler
{
    public abstract string Type { get; }

    public async Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken)
    {
        TPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<TPayload>(context.PayloadJson)
                ?? throw new JsonException("Payload deserialised to null.");
        }
        catch (JsonException)
        {
            return JobHandlerResult.Failed("Invalid JSON payload.");
        }

        return await HandleTypedAsync(context, payload, cancellationToken);
    }

    protected abstract Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, TPayload payload, CancellationToken cancellationToken);
}

public sealed record JobContext(int JobId, string Type, string PayloadJson);

public sealed record JobHandlerResult(bool Success, string ResultJson, string? ErrorMessage)
{
    public static JobHandlerResult Succeeded(string resultJson = "{}") => new(true, resultJson, null);
    public static JobHandlerResult Failed(string errorMessage, string resultJson = "{}") =>
        new(false, resultJson, errorMessage);
}
