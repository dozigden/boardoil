using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Realtime;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BoardOil.Api.Endpoints;

public static class InternalRealtimeEndpoints
{
    public static IEndpointRouteBuilder MapInternalRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(BoardRealtimeRelay.EndpointPath, ForwardBoardEventAsync);
        return app;
    }

    private static async Task<IResult> ForwardBoardEventAsync(
        HttpContext httpContext,
        BoardRealtimeRelayEvent request,
        BoardOilInternalOptions options,
        IBoardEvents boardEvents)
    {
        if (!IsEndpointEnabled(options))
        {
            return ((ApiResult)ApiErrors.NotFound("Not found.")).ToHttpResult();
        }

        if (!IsAuthorised(httpContext, options))
        {
            return ((ApiResult)ApiErrors.Unauthorized("Unauthorised.")).ToHttpResult();
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ((ApiResult)validationError).ToHttpResult();
        }

        await DispatchAsync(request, boardEvents);
        return ApiResults.Ok().ToHttpResult();
    }

    private static bool IsEndpointEnabled(BoardOilInternalOptions options) =>
        !string.IsNullOrWhiteSpace(options.McpEventRelayApiKey)
        || options.McpEventRelayAllowedSourceIps.Count > 0;

    private static bool IsAuthorised(HttpContext httpContext, BoardOilInternalOptions options)
    {
        if (IsApiKeyAuthorised(httpContext, options))
        {
            return true;
        }

        return IsSourceIpAuthorised(httpContext, options);
    }

    private static bool IsApiKeyAuthorised(HttpContext httpContext, BoardOilInternalOptions options)
    {
        var expected = options.McpEventRelayApiKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (!httpContext.Request.Headers.TryGetValue(BoardRealtimeRelay.ApiKeyHeaderName, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static bool IsSourceIpAuthorised(HttpContext httpContext, BoardOilInternalOptions options)
    {
        if (options.McpEventRelayAllowedSourceIps.Count == 0)
        {
            return false;
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        var normalisedRemoteIp = Normalise(remoteIp);

        if (IPAddress.IsLoopback(normalisedRemoteIp))
        {
            return options.McpEventRelayAllowedSourceIps.Any(IPAddress.IsLoopback);
        }

        return options.McpEventRelayAllowedSourceIps.Any(allowedIp => allowedIp.Equals(normalisedRemoteIp));
    }

    private static IPAddress Normalise(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static ApiError? Validate(BoardRealtimeRelayEvent request)
    {
        if (request.BoardId <= 0)
        {
            return ApiErrors.BadRequest("BoardId must be greater than zero.");
        }

        return request.EventType switch
        {
            BoardRealtimeEventTypes.ColumnCreated => request.Column is null
                ? ApiErrors.BadRequest("Column payload is required for column_created.")
                : null,
            BoardRealtimeEventTypes.ColumnUpdated => request.Column is null
                ? ApiErrors.BadRequest("Column payload is required for column_updated.")
                : null,
            BoardRealtimeEventTypes.ColumnDeleted => request.ColumnId is null
                ? ApiErrors.BadRequest("ColumnId is required for column_deleted.")
                : null,
            BoardRealtimeEventTypes.CardCreated => request.Card is null
                ? ApiErrors.BadRequest("Card payload is required for card_created.")
                : null,
            BoardRealtimeEventTypes.CardUpdated => request.Card is null
                ? ApiErrors.BadRequest("Card payload is required for card_updated.")
                : null,
            BoardRealtimeEventTypes.CardDeleted => request.CardId is null
                ? ApiErrors.BadRequest("CardId is required for card_deleted.")
                : null,
            BoardRealtimeEventTypes.CardMoved => request.Card is null
                ? ApiErrors.BadRequest("Card payload is required for card_moved.")
                : null,
            _ => ApiErrors.BadRequest("Unsupported realtime event type.")
        };
    }

    private static Task DispatchAsync(BoardRealtimeRelayEvent request, IBoardEvents boardEvents) =>
        request.EventType switch
        {
            BoardRealtimeEventTypes.ColumnCreated => boardEvents.ColumnCreatedAsync(request.BoardId, request.Column!),
            BoardRealtimeEventTypes.ColumnUpdated => boardEvents.ColumnUpdatedAsync(request.BoardId, request.Column!),
            BoardRealtimeEventTypes.ColumnDeleted => boardEvents.ColumnDeletedAsync(request.BoardId, request.ColumnId!.Value),
            BoardRealtimeEventTypes.CardCreated => boardEvents.CardCreatedAsync(request.BoardId, request.Card!),
            BoardRealtimeEventTypes.CardUpdated => boardEvents.CardUpdatedAsync(request.BoardId, request.Card!),
            BoardRealtimeEventTypes.CardDeleted => boardEvents.CardDeletedAsync(request.BoardId, request.CardId!.Value),
            BoardRealtimeEventTypes.CardMoved => boardEvents.CardMovedAsync(request.BoardId, request.Card!),
            _ => Task.CompletedTask
        };
}
