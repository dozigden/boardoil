using BoardOil.Contracts.CustomField;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoardOil.Api.Swagger;

internal sealed class CustomFieldRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requestType = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAcceptsMetadata>().LastOrDefault()?.RequestType;
        if (requestType != typeof(CustomFieldDto) && requestType != typeof(CustomFieldPreviewContract)) { return; }

        // Runtime input models retain invalid scalars; document the accepted, typed wire contract.
        if (operation.RequestBody?.Content?.TryGetValue("application/json", out var content) == true)
        {
            content.Schema = context.SchemaGenerator.GenerateSchema(requestType, context.SchemaRepository);
        }
    }
}
