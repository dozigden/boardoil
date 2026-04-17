using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

public sealed class NonNullableRequestSchemaFilter : ISchemaFilter
{
    private static readonly NullabilityInfoContext NullabilityInfoContext = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type is null
            || !context.Type.Name.EndsWith("Request", StringComparison.Ordinal)
            || schema.Properties.Count == 0)
        {
            return;
        }

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType.IsValueType)
            {
                continue;
            }

            var nullability = NullabilityInfoContext.Create(property);
            if (nullability.ReadState != NullabilityState.NotNull)
            {
                continue;
            }

            var schemaPropertyName = ResolveSchemaPropertyName(property);
            if (!schema.Properties.TryGetValue(schemaPropertyName, out var propertySchema))
            {
                continue;
            }

            propertySchema.Nullable = false;
            schema.Required.Add(schemaPropertyName);
        }
    }

    private static string ResolveSchemaPropertyName(PropertyInfo property)
    {
        var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(jsonPropertyName))
        {
            return jsonPropertyName;
        }

        return JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    }
}
