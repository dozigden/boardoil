using BoardOil.Contracts.CustomField;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

public sealed class CustomFieldRequestSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema { Properties: not null } mutableSchema) { return; }

        // Requests retain invalid JSON scalars for validation; document the valid wire types.
        if (context.Type == typeof(TextCustomFieldDefinitionInput))
        {
            SetNullableScalar(mutableSchema, "minLength", JsonSchemaType.Integer, "int32");
            SetNullableScalar(mutableSchema, "maxLength", JsonSchemaType.Integer, "int32");
        }
        else if (context.Type == typeof(IntegerCustomFieldDefinitionInput))
        {
            SetNullableScalar(mutableSchema, "defaultValue", JsonSchemaType.Integer, "int32");
            SetNullableScalar(mutableSchema, "min", JsonSchemaType.Integer, "int32");
            SetNullableScalar(mutableSchema, "max", JsonSchemaType.Integer, "int32");
        }
        else if (context.Type == typeof(DateCustomFieldDefinitionInput))
        {
            SetNullableScalar(mutableSchema, "defaultValue", JsonSchemaType.String, "date");
        }
    }

    private static void SetNullableScalar(OpenApiSchema schema, string property, JsonSchemaType type, string format) =>
        schema.Properties![property] = new OpenApiSchema { Type = type | JsonSchemaType.Null, Format = format };
}
