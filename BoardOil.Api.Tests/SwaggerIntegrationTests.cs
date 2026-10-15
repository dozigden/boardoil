using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SwaggerIntegrationTests
    : ApiFactoryIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public SwaggerIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Theory]
    [InlineData("/api/boards/{boardId}/card-types", "post", "CreateCardTypeRequest")]
    [InlineData("/api/boards/{boardId}/card-types/{cardTypeId}", "put", "UpdateCardTypeRequest")]
    [InlineData("/api/boards/{boardId}/custom-fields", "post", "CustomFieldRequest")]
    [InlineData("/api/boards/{boardId}/custom-fields/{customFieldId}", "put", "CustomFieldRequest")]
    [InlineData("/api/boards/{boardId}/custom-fields/{customFieldId}/preview", "post", "CustomFieldPreviewRequest")]
    public async Task FieldAndCardTypeWrites_ShouldDocumentTheirRequestTypes(string path, string method, string schema)
    {
        var client = CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        var body = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal($"#/components/schemas/{schema}", body.GetProperty("$ref").GetString());
    }

    [Theory]
    [InlineData("TextCustomFieldDefinitionInput", "minLength", "integer", "int32")]
    [InlineData("TextCustomFieldDefinitionInput", "maxLength", "integer", "int32")]
    [InlineData("IntegerCustomFieldDefinitionInput", "defaultValue", "integer", "int32")]
    [InlineData("IntegerCustomFieldDefinitionInput", "min", "integer", "int32")]
    [InlineData("IntegerCustomFieldDefinitionInput", "max", "integer", "int32")]
    [InlineData("DateCustomFieldDefinitionInput", "defaultValue", "string", "date")]
    public async Task CustomFieldRequests_ShouldDocumentTypedNullableScalars(string type, string property, string wireType, string format)
    {
        var client = CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var scalar = schemas.GetProperty(type).GetProperty("properties").GetProperty(property);
        Assert.Equal(wireType, scalar.GetProperty("type").GetString());
        Assert.Equal(format, scalar.GetProperty("format").GetString());
        Assert.True(scalar.GetProperty("nullable").GetBoolean());
        var request = schemas.GetProperty("CustomFieldRequest");
        Assert.Contains(request.GetProperty("required").EnumerateArray(), item => item.GetString() == "definition");
        Assert.Contains(request.GetProperty("required").EnumerateArray(), item => item.GetString() == "appliesTo");
    }

    [Fact]
    public async Task CustomFieldRequests_ShouldDocumentDefinitionAndApplicabilityVariants()
    {
        var client = CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var properties = schemas.GetProperty("CustomFieldRequest").GetProperty("properties");
        var definition = properties.GetProperty("definition");
        Assert.Equal("dataType", schemas.GetProperty("CustomFieldDefinitionInput").GetProperty("discriminator").GetProperty("propertyName").GetString());
        Assert.Equal(new[] { "DateCustomFieldDefinitionInput", "IntegerCustomFieldDefinitionInput", "TextCustomFieldDefinitionInput" },
            definition.GetProperty("oneOf").EnumerateArray().Select(item => item.GetProperty("$ref").GetString()!.Split('/')[^1]).Order());
        var applicability = properties.GetProperty("appliesTo");
        Assert.Equal("mode", schemas.GetProperty("CustomFieldApplicability").GetProperty("discriminator").GetProperty("propertyName").GetString());
        Assert.Equal(new[] { "AllCardTypes", "SelectedCardTypes" },
            applicability.GetProperty("oneOf").EnumerateArray().Select(item => item.GetProperty("$ref").GetString()!.Split('/')[^1]).Order());
        Assert.Equal("#/components/schemas/CustomFieldRequest",
            schemas.GetProperty("CustomFieldPreviewRequest").GetProperty("properties").GetProperty("field").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task Document_ShouldRemainOpenApi30WithJwtAndPatBearerDefinitions()
    {
        // Arrange
        var client = CreateClient();

        // Act
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");

        // Assert
        Assert.StartsWith("3.0.", root.GetProperty("openapi").GetString());
        Assert.Equal("BoardOil API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal(2, schemes.EnumerateObject().Count());
        AssertBearerScheme(schemes.GetProperty("Bearer"), "JWT");
        AssertBearerScheme(schemes.GetProperty("PatBearer"), "PAT");
        Assert.All(root.GetProperty("paths").EnumerateObject(), path => Assert.StartsWith("/api/", path.Name));
    }

    [Theory]
    [InlineData("/api/boards", "get", MachinePatScopes.ApiRead)]
    [InlineData("/api/boards", "post", MachinePatScopes.ApiWrite)]
    [InlineData("/api/boards/{boardId}/cards/search", "post", MachinePatScopes.ApiRead)]
    [InlineData("/api/system/boards", "get", MachinePatScopes.ApiSystem)]
    public async Task ProtectedOperation_ShouldOfferJwtOrPatWithRequiredScope(string path, string method, string scope)
    {
        // Arrange
        var client = CreateClient();

        // Act
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        var requirements = operation.GetProperty("security").EnumerateArray().ToArray();

        // Assert
        Assert.Equal(2, requirements.Length);
        Assert.Empty(Assert.Single(requirements, requirement => requirement.TryGetProperty("Bearer", out _))
            .GetProperty("Bearer").EnumerateArray());
        Assert.Empty(Assert.Single(requirements, requirement => requirement.TryGetProperty("PatBearer", out _))
            .GetProperty("PatBearer").EnumerateArray());
        Assert.All(requirements, requirement => Assert.Single(requirement.EnumerateObject()));
        Assert.Equal(scope, Assert.Single(operation.GetProperty("x-pat-scopes").EnumerateArray()).GetString());
        Assert.Contains($"Required access token scope: `{scope}`.", operation.GetProperty("description").GetString());
    }

    [Fact]
    public async Task AccessTokenManagement_ShouldOnlyOfferJwtAuthentication()
    {
        // Arrange
        var client = CreateClient();

        // Act
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/auth/access-tokens").GetProperty("post");
        var requirement = Assert.Single(operation.GetProperty("security").EnumerateArray());

        // Assert
        Assert.Equal("Bearer", Assert.Single(requirement.EnumerateObject()).Name);
        Assert.Empty(requirement.GetProperty("Bearer").EnumerateArray());
        Assert.False(operation.TryGetProperty("x-pat-scopes", out _));
        Assert.Contains("Access tokens cannot call this endpoint.", operation.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Login_ShouldRemainDocumentedWithoutAuthenticationRequirements()
    {
        // Arrange
        var client = CreateClient();

        // Act
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");

        // Assert
        Assert.False(operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        Assert.False(operation.TryGetProperty("x-pat-scopes", out _));
    }

    [Fact]
    public async Task DocumentShortcut_ShouldRedirectToExistingDocumentRoute()
    {
        // Arrange
        var client = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

        // Act
        var response = await client.GetAsync("/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger/v1/swagger.json", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task SwaggerUi_ShouldServeItsPageAnonymously()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("swagger-ui-bundle.js", html);
        Assert.Contains("index.js", html);
    }

    [Fact]
    public async Task SwaggerUi_ShouldUseExistingDocumentRouteAndOmitCookieCredentials()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var script = await client.GetStringAsync("/swagger/index.js");
        var interceptorsMatch = Regex.Match(script, "var interceptors = JSON.parse\\('(.+)'\\);");

        // Assert
        Assert.Contains("/swagger/v1/swagger.json", script);
        Assert.True(interceptorsMatch.Success);
        using var interceptors = JsonDocument.Parse(interceptorsMatch.Groups[1].Value);
        Assert.Equal("(req) => { req.credentials = 'omit'; return req; }",
            interceptors.RootElement.GetProperty("RequestInterceptorFunction").GetString());
    }

    [Theory]
    [InlineData("swagger-ui-bundle.js")]
    [InlineData("swagger-ui-standalone-preset.js")]
    [InlineData("swagger-ui.css")]
    public async Task SwaggerUi_ShouldServeBundledAssets(string asset)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/swagger/{asset}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    private static void AssertBearerScheme(JsonElement scheme, string format)
    {
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal(format, scheme.GetProperty("bearerFormat").GetString());
    }
}
