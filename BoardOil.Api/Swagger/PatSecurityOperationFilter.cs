using BoardOil.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
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
        AddSecurityRequirement(operation, JwtSchemeName);

        if (pathString.StartsWithSegments("/api/auth/access-tokens", StringComparison.OrdinalIgnoreCase))
        {
            AppendPatNotes(operation, ["PATs cannot call this endpoint."]);
            return;
        }

        var httpMethod = context.ApiDescription.HttpMethod ?? HttpMethods.Get;
        var requiredScope = PatApiScopeRules.GetRequiredScope(httpMethod, pathString);

        AddSecurityRequirement(operation, PatSchemeName);
        operation.Extensions["x-pat-scopes"] = new OpenApiArray { new OpenApiString(requiredScope) };

        var notes = new List<string>
        {
            $"Required PAT scope: `{requiredScope}`."
        };

        if (HasBoardIdParameter(context))
        {
            notes.Add("Selected-board PATs can only access allow-listed boardIds.");
        }

        if (IsBoardsList(pathString, httpMethod))
        {
            notes.Add("Selected-board PATs only see allow-listed boards.");
        }

        if (IsBoardCreate(pathString, httpMethod))
        {
            notes.Add("Selected-board PATs cannot create or import boards.");
        }

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

    private static bool HasBoardIdParameter(OperationFilterContext context) =>
        context.ApiDescription.ParameterDescriptions.Any(parameter =>
            string.Equals(parameter.Name, "boardId", StringComparison.OrdinalIgnoreCase));

    private static bool IsBoardsList(PathString path, string httpMethod) =>
        HttpMethods.IsGet(httpMethod)
        && path.Equals("/api/boards", StringComparison.OrdinalIgnoreCase);

    private static bool IsBoardCreate(PathString path, string httpMethod) =>
        HttpMethods.IsPost(httpMethod)
        && (path.Equals("/api/boards", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/boards/import", StringComparison.OrdinalIgnoreCase));

    private static void AddSecurityRequirement(OpenApiOperation operation, string schemeName)
    {
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = schemeName
                    }
                }
            ] = Array.Empty<string>()
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

        var lines = new List<string> { "PAT notes:" };
        lines.AddRange(noteList.Select(note => $"- {note}"));
        var block = string.Join('\n', lines);

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? block
            : $"{operation.Description}\n\n{block}";
    }
}
