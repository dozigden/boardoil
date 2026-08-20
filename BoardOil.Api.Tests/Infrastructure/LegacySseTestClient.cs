using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BoardOil.Api.Tests.Infrastructure;

internal sealed class LegacySseTestClient : IAsyncDisposable
{
    private readonly HttpClient client;
    private readonly string bearerToken;
    private readonly HttpResponseMessage streamResponse;
    private readonly StreamReader streamReader;

    private LegacySseTestClient(
        HttpClient client,
        string bearerToken,
        Uri messageEndpoint,
        HttpResponseMessage streamResponse,
        StreamReader streamReader)
    {
        this.client = client;
        this.bearerToken = bearerToken;
        this.streamResponse = streamResponse;
        this.streamReader = streamReader;
        MessageEndpoint = messageEndpoint;
    }

    public Uri MessageEndpoint { get; }

    public string? MediaType => streamResponse.Content.Headers.ContentType?.MediaType;

    public static async Task<LegacySseTestClient> ConnectAsync(
        HttpClient client,
        string sseEndpoint,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sseEndpoint);
        request.Headers.Authorization = new("Bearer", bearerToken);
        request.Headers.Accept.ParseAdd("text/event-stream");

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException(
                $"Legacy SSE connection returned {(int)response.StatusCode}: {responseBody}");
        }

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var reader = new StreamReader(responseStream);
        try
        {
            var endpointEvent = await ReadEventAsync(reader, cancellationToken);
            if (!string.Equals(endpointEvent.Name, "endpoint", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Expected the first SSE event to be 'endpoint', but received '{endpointEvent.Name}'.");
            }

            var messageEndpoint = ResolveMessageEndpoint(client, sseEndpoint, endpointEvent.Data);
            return new LegacySseTestClient(
                client,
                bearerToken,
                messageEndpoint,
                response,
                reader);
        }
        catch
        {
            reader.Dispose();
            response.Dispose();
            throw;
        }
    }

    public async Task<HttpResponseMessage> SendMessageAsync(
        object message,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, MessageEndpoint)
        {
            Content = JsonContent.Create(message)
        };
        request.Headers.Authorization = new("Bearer", bearerToken);
        return await client.SendAsync(request, cancellationToken);
    }

    public async Task<JsonDocument> ReadResponseAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var messageEvent = await ReadEventAsync(streamReader, cancellationToken);
            if (!string.Equals(messageEvent.Name, "message", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = JsonDocument.Parse(messageEvent.Data);
            if (payload.RootElement.TryGetProperty("id", out var id)
                && string.Equals(id.GetString(), requestId, StringComparison.Ordinal))
            {
                return payload;
            }

            payload.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        streamReader.Dispose();
        streamResponse.Dispose();
        return ValueTask.CompletedTask;
    }

    private static Uri ResolveMessageEndpoint(
        HttpClient client,
        string sseEndpoint,
        string advertisedEndpoint)
    {
        var baseAddress = client.BaseAddress
            ?? throw new InvalidOperationException("The test client must have a base address.");
        var messageEndpoint = new Uri(baseAddress, advertisedEndpoint);
        if (!string.Equals(messageEndpoint.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(messageEndpoint.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || messageEndpoint.Port != baseAddress.Port)
        {
            throw new InvalidDataException("The SSE message endpoint must use the test server origin.");
        }

        var sseUri = new Uri(baseAddress, sseEndpoint);
        var messageDirectory = sseUri.AbsolutePath[..(sseUri.AbsolutePath.LastIndexOf('/') + 1)];
        var expectedMessagePath = $"{messageDirectory}message";
        if (!string.Equals(messageEndpoint.AbsolutePath, expectedMessagePath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Expected the SSE message path '{expectedMessagePath}', but received '{messageEndpoint.AbsolutePath}'.");
        }

        if (string.IsNullOrWhiteSpace(messageEndpoint.Query)
            || !messageEndpoint.Query.Contains("sessionId=", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The SSE message endpoint did not contain a session ID.");
        }

        return messageEndpoint;
    }

    private static async Task<SseEvent> ReadEventAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        string? eventName = null;
        var dataLines = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (dataLines.Count > 0)
                {
                    return new SseEvent(eventName ?? "message", string.Join('\n', dataLines));
                }

                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            var field = separatorIndex < 0 ? line : line[..separatorIndex];
            var value = separatorIndex < 0 ? string.Empty : line[(separatorIndex + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            if (string.Equals(field, "event", StringComparison.Ordinal))
            {
                eventName = value;
            }
            else if (string.Equals(field, "data", StringComparison.Ordinal))
            {
                dataLines.Add(value);
            }
        }

        throw new EndOfStreamException("The legacy SSE stream ended before the expected event arrived.");
    }

    private sealed record SseEvent(string Name, string Data);
}
