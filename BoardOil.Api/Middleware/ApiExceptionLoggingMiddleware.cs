using System.Security.Claims;
using System.Text.Json;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.Auth;
using BoardOil.Contracts.Common;

namespace BoardOil.Api.Middleware;

public sealed class ApiExceptionLoggingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IErrorLogService errorLogService)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (BadHttpRequestException exception) when (IsApiRequest(context))
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteApiErrorAsync(context, exception.StatusCode, "Invalid request.");
        }
        catch (Exception exception) when (IsApiRequest(context))
        {
            logger.LogError(
                exception,
                "Unhandled API exception for {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            var errorLogId = await errorLogService.LogExceptionAsync(
                exception,
                new ErrorLogContext(
                    ErrorLogSources.Backend,
                    ErrorLogAreas.ApiRequest,
                    TraceIdentifier: context.TraceIdentifier,
                    RequestMethod: context.Request.Method,
                    RequestPath: context.Request.Path.Value,
                    ActorUserId: TryGetActorUserId(context),
                    ContextJson: BuildContextJson(context)),
                CancellationToken.None);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var errorReference = errorLogId?.ToString() ?? context.TraceIdentifier;
            await WriteApiErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                $"An unexpected server error occurred. Error reference: {errorReference}.");
        }
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static int? TryGetActorUserId(HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextActorUserExtensions.ActorUserIdItemKey, out var value)
            && value is int actorUserId)
        {
            return actorUserId;
        }

        return int.TryParse(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var claimUserId)
            ? claimUserId
            : null;
    }

    private static string BuildContextJson(HttpContext context) =>
        JsonSerializer.Serialize(new
        {
            endpoint = context.GetEndpoint()?.DisplayName
        });

    private static async Task WriteApiErrorAsync(
        HttpContext context,
        int statusCode,
        string message)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            new ApiResult(false, statusCode, message),
            context.RequestAborted);
    }
}
