using System.Net.Http.Json;
using System.Text.Json;

namespace BoardOil.Api.Tests.Infrastructure;

internal static class McpJsonRpcClient
{
    public static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        string method,
        object @params,
        string id,
        string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = @params
            })
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new("Bearer", bearerToken);
        }

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await client.SendAsync(request);
    }

    public static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var trimmed = content.TrimStart();

        if (trimmed.StartsWith('{'))
        {
            return JsonDocument.Parse(trimmed);
        }

        var sseJsonPayload = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => line["data:".Length..].Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .LastOrDefault();
        if (sseJsonPayload is not null)
        {
            return JsonDocument.Parse(sseJsonPayload);
        }

        throw new JsonException($"MCP response was not parseable JSON: {content}");
    }

    public static JsonElement GetStructuredContent(JsonDocument payload) =>
        payload.RootElement
            .GetProperty("result")
            .GetProperty("structuredContent");

    public static JsonElement GetToolByName(JsonDocument payload, string toolName) =>
        payload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Single(tool => string.Equals(tool.GetProperty("name").GetString(), toolName, StringComparison.Ordinal));
}
