using System.Text.Json.Nodes;
using BoardOil.Contracts.Card;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

public sealed class CardSearchSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema mutableSchema)
        {
            return;
        }

        if (context.Type == typeof(SearchCardsRequest))
        {
            ApplySearchRequestSchema(mutableSchema);
            return;
        }

        if (context.Type == typeof(CardSearchFilterRequest))
        {
            ApplySearchFilterSchema(mutableSchema);
        }
    }

    private static void ApplySearchRequestSchema(OpenApiSchema schema)
    {
        if (schema.Properties is null
            || !schema.Properties.TryGetValue("filters", out var propertySchema)
            || propertySchema is not OpenApiSchema filtersSchema)
        {
            return;
        }

        filtersSchema.Description = "Filters to apply. Every filter must match for a card to be returned.";
        filtersSchema.MinItems = CardSearchLimits.MinimumFilterCount;
        filtersSchema.MaxItems = CardSearchLimits.MaximumFilterCount;
        schema.Example = new JsonObject
        {
            ["filters"] = new JsonArray
            {
                new JsonObject
                {
                    ["field"] = CardSearchFields.ExternalUrl,
                    ["operator"] = CardSearchOperators.Contains,
                    ["value"] = "github.com/example/repository"
                }
            }
        };
    }

    private static void ApplySearchFilterSchema(OpenApiSchema schema)
    {
        if (schema.Properties is null)
        {
            return;
        }

        if (schema.Properties.TryGetValue("field", out var field) && field is OpenApiSchema fieldSchema)
        {
            fieldSchema.Description = "Card field to search.";
            fieldSchema.Enum = [JsonValue.Create(CardSearchFields.ExternalUrl)];
            fieldSchema.Example = JsonValue.Create(CardSearchFields.ExternalUrl);
        }

        if (schema.Properties.TryGetValue("operator", out var matchOperator) && matchOperator is OpenApiSchema operatorSchema)
        {
            operatorSchema.Description = "Match operator to apply to the field value.";
            operatorSchema.Enum =
            [
                JsonValue.Create(CardSearchOperators.Exact),
                JsonValue.Create(CardSearchOperators.Contains)
            ];
            operatorSchema.Example = JsonValue.Create(CardSearchOperators.Contains);
        }

        if (schema.Properties.TryGetValue("value", out var value) && value is OpenApiSchema valueSchema)
        {
            valueSchema.Description = "Non-empty value to match.";
            valueSchema.MinLength = 1;
            valueSchema.Example = JsonValue.Create("github.com/example/repository");
        }
    }
}
