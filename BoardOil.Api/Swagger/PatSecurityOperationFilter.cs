using System.Text.Json.Nodes;
using BoardOil.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

internal sealed class PatSecurityOperationFilter : IOperationFilter
{
    private const string JwtSchemeName = "Bearer";
    private const string PatSchemeName = "PatBearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        if (!metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        var path = GetPath(context.ApiDescription.RelativePath);
        var pathString = new PathString(path);
        if (!pathString.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Security ??= [];
        AddSecurityRequirement(operation, context.Document, JwtSchemeName);

        if (pathString.StartsWithSegments("/api/auth/access-tokens", StringComparison.OrdinalIgnoreCase))
        {
            AppendPatNotes(operation, ["Access tokens cannot call this endpoint."]);
            return;
        }

        var httpMethod = context.ApiDescription.HttpMethod ?? HttpMethods.Get;
        var requiredScope = PatApiScopeRules.GetRequiredScope(httpMethod, pathString);

        AddSecurityRequirement(operation, context.Document, PatSchemeName);
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-pat-scopes"] = new JsonNodeExtension(new JsonArray(requiredScope));

        var notes = new List<string>
        {
            $"Required access token scope: `{requiredScope}`."
        };

        AppendPatNotes(operation, notes);
    }

    private static string GetPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        var trimmed = relativePath.Split('?', 2)[0].TrimStart('/');
        return $"/{trimmed}";
    }

    private static void AddSecurityRequirement(OpenApiOperation operation, OpenApiDocument document, string schemeName)
    {
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(schemeName, document)] = []
        });
    }

    private static void AppendPatNotes(OpenApiOperation operation, IEnumerable<string> notes)
    {
        var noteList = notes
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .ToList();
        if (noteList.Count == 0)
        {
            return;
        }

        var lines = new List<string> { "Access token notes:" };
        lines.AddRange(noteList.Select(note => $"- {note}"));
        var block = string.Join('\n', lines);

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? block
            : $"{operation.Description}\n\n{block}";
    }
}
