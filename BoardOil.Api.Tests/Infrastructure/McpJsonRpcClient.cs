using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BoardOil.Api.Tests.Infrastructure;

internal static class McpJsonRpcClient
{
    public const string ModernProtocolVersion = "2026-07-28";
    public const string LegacyProtocolVersion = "2025-11-25";

    public static Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        string method,
        object @params,
        string id,
        string? bearerToken = null,
        string endpoint = "/mcp") =>
        SendModernRequestAsync(client, method, @params, id, bearerToken, endpoint);

    public static Task<HttpResponseMessage> SendModernRequestAsync(
        HttpClient client,
        string method,
        object @params,
        string id,
        string? bearerToken = null,
        string endpoint = "/mcp",
        string protocolVersionHeader = ModernProtocolVersion,
        string protocolVersionMetadata = ModernProtocolVersion,
        string? routingMethod = null,
        bool includeRoutingMethod = true,
        bool includeRoutingName = true)
    {
        var requestParams = JsonSerializer.SerializeToNode(@params) as JsonObject ?? [];
        requestParams["_meta"] = new JsonObject
        {
            ["io.modelcontextprotocol/protocolVersion"] = protocolVersionMetadata,
            ["io.modelcontextprotocol/clientInfo"] = new JsonObject
            {
                ["name"] = "BoardOil integration tests",
                ["version"] = "1.0.0"
            },
            ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject()
        };

        return SendAsync(
            client,
            method,
            requestParams,
            id,
            bearerToken,
            endpoint,
            protocolVersionHeader,
            sessionId: null,
            routingMethod: includeRoutingMethod ? routingMethod ?? method : null,
            includeRoutingName: includeRoutingName);
    }

    public static Task<HttpResponseMessage> SendLegacyInitializeAsync(
        HttpClient client,
        string id,
        string? bearerToken = null,
        string endpoint = "/mcp") =>
        SendAsync(
            client,
            "initialize",
            new JsonObject
            {
                ["protocolVersion"] = LegacyProtocolVersion,
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "BoardOil legacy integration tests",
                    ["version"] = "1.0.0"
                }
            },
            id,
            bearerToken,
            endpoint,
            protocolVersion: null,
            sessionId: null,
            routingMethod: null,
            includeRoutingName: false);

    public static Task<HttpResponseMessage> SendLegacyRequestAsync(
        HttpClient client,
        string method,
        object @params,
        string id,
        string? bearerToken = null,
        string endpoint = "/mcp",
        string? sessionId = null) =>
        SendAsync(
            client,
            method,
            JsonSerializer.SerializeToNode(@params) as JsonObject ?? [],
            id,
            bearerToken,
            endpoint,
            LegacyProtocolVersion,
            sessionId,
            routingMethod: null,
            includeRoutingName: false);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string method,
        JsonObject requestParams,
        string id,
        string? bearerToken,
        string endpoint,
        string? protocolVersion,
        string? sessionId,
        string? routingMethod,
        bool includeRoutingName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = requestParams
            })
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new("Bearer", bearerToken);
        }

        if (protocolVersion is not null)
        {
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", protocolVersion);
        }

        if (sessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        }

        if (routingMethod is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Method", routingMethod);
        }

        if (includeRoutingName)
        {
            var requestName = GetRequestName(method, requestParams);
            if (requestName is not null)
            {
                request.Headers.TryAddWithoutValidation("Mcp-Name", requestName);
            }
        }

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await client.SendAsync(request);
    }

    private static string? GetRequestName(string method, JsonObject requestParams)
    {
        if (string.Equals(method, "tools/call", StringComparison.Ordinal)
            || string.Equals(method, "prompts/get", StringComparison.Ordinal))
        {
            return requestParams["name"]?.GetValue<string>();
        }

        if (string.Equals(method, "resources/read", StringComparison.Ordinal))
        {
            return requestParams["uri"]?.GetValue<string>();
        }

        return null;
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
