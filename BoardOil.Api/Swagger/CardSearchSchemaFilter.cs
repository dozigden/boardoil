using BoardOil.Contracts.Card;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

public sealed class CardSearchSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(SearchCardsRequest))
        {
            ApplySearchRequestSchema(schema);
            return;
        }

        if (context.Type == typeof(CardSearchFilterRequest))
        {
            ApplySearchFilterSchema(schema);
        }
    }

    private static void ApplySearchRequestSchema(OpenApiSchema schema)
    {
        if (!schema.Properties.TryGetValue("filters", out var filtersSchema))
        {
            return;
        }

        filtersSchema.Description = "Filters to apply. Every filter must match for a card to be returned.";
        filtersSchema.MinItems = CardSearchLimits.MinimumFilterCount;
        filtersSchema.MaxItems = CardSearchLimits.MaximumFilterCount;
        schema.Example = new OpenApiObject
        {
            ["filters"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["field"] = new OpenApiString(CardSearchFields.ExternalUrl),
                    ["operator"] = new OpenApiString(CardSearchOperators.Contains),
                    ["value"] = new OpenApiString("github.com/example/repository")
                }
            }
        };
    }

    private static void ApplySearchFilterSchema(OpenApiSchema schema)
    {
        if (schema.Properties.TryGetValue("field", out var fieldSchema))
        {
            fieldSchema.Description = "Card field to search.";
            fieldSchema.Enum = [new OpenApiString(CardSearchFields.ExternalUrl)];
            fieldSchema.Example = new OpenApiString(CardSearchFields.ExternalUrl);
        }

        if (schema.Properties.TryGetValue("operator", out var operatorSchema))
        {
            operatorSchema.Description = "Match operator to apply to the field value.";
            operatorSchema.Enum =
            [
                new OpenApiString(CardSearchOperators.Exact),
                new OpenApiString(CardSearchOperators.Contains)
            ];
            operatorSchema.Example = new OpenApiString(CardSearchOperators.Contains);
        }

        if (schema.Properties.TryGetValue("value", out var valueSchema))
        {
            valueSchema.Description = "Non-empty value to match.";
            valueSchema.MinLength = 1;
            valueSchema.Example = new OpenApiString("github.com/example/repository");
        }
    }
}
