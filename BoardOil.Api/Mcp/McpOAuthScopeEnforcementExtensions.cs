using System.Text.Json;

namespace BoardOil.Api.Mcp;

public static class McpOAuthScopeEnforcementExtensions
{
    public static WebApplication UseMcpOAuthScopeEnforcement(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsPost(context.Request.Method)
                || !context.Request.Path.StartsWithSegments(
                    "/mcp/connections",
                    StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var authorisationService = context.RequestServices
                .GetRequiredService<IMcpAuthorisationService>();
            var accessContext = authorisationService.GetAccessContext(context.User);
            if (accessContext is null
                || !string.Equals(
                    accessContext.AuthenticationType,
                    "OAuth",
                    StringComparison.Ordinal))
            {
                await next();
                return;
            }

            var inspection = await InspectRequestAsync(context.Request, context.RequestAborted);
            if (inspection.HasInvalidFieldType)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var toolRegistry = context.RequestServices.GetRequiredService<McpToolRegistry>();
            string? requiredScope = null;
            if (inspection.ToolName is not null
                && toolRegistry.TryGetRegistration(inspection.ToolName, out var registration))
            {
                requiredScope = registration.Definition.RequiredScope;
            }

            if (requiredScope is null || accessContext.Scopes.Contains(requiredScope))
            {
                await next();
                return;
            }

            McpOAuthChallengeState.MarkInsufficientScope(context, requiredScope);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        });

        return app;
    }

    private static async Task<McpToolRequestInspection> InspectRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        try
        {
            using var document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("method", out var method))
            {
                return McpToolRequestInspection.NotToolCall;
            }

            if (method.ValueKind != JsonValueKind.String)
            {
                return McpToolRequestInspection.InvalidFieldType;
            }

            if (!string.Equals(method.GetString(), "tools/call", StringComparison.Ordinal)
                || !root.TryGetProperty("params", out var parameters)
                || parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("name", out var name))
            {
                return McpToolRequestInspection.NotToolCall;
            }

            return name.ValueKind == JsonValueKind.String
                ? McpToolRequestInspection.ForTool(name.GetString())
                : McpToolRequestInspection.InvalidFieldType;
        }
        catch (JsonException)
        {
            return McpToolRequestInspection.NotToolCall;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private sealed record McpToolRequestInspection(
        bool HasInvalidFieldType,
        string? ToolName)
    {
        public static McpToolRequestInspection NotToolCall { get; } = new(false, null);

        public static McpToolRequestInspection InvalidFieldType { get; } = new(true, null);

        public static McpToolRequestInspection ForTool(string? toolName) => new(false, toolName);
    }
}
