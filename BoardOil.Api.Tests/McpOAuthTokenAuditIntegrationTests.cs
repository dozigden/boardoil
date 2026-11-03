using System.Net;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpOAuthTokenAuditIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    private static readonly DateTime Occurred = new(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly string Fingerprint = "sha256:" + new string('a', 64);

    public McpOAuthTokenAuditIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task AuditList_ShouldExposeOnlyAllowedFieldsInSchemaAndBothResponseRepresentations()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        var capture = Factory.Services.GetRequiredService<OAuthTokenAuditCaptureState>();
        Assert.False(capture.IsEnabled);
        var audit = new EntityOAuthTokenAudit
        {
            OccurredAtUtc = Occurred, Outcome = "Rejected", GrantType = "refresh_token",
            RequestedScopes = "mcp:read", ErrorCode = "invalid_grant",
            PresentedTokenFingerprint = Fingerprint, IssuedRefreshTokenFingerprint = null,
            AuthorizationId = "authorization-1", OAuthClientId = "client-1", OAuthConnectionId = 7,
            TraceIdentifier = "trace-1",
            ErrorDescription = "excluded-secret-description", ErrorUri = "https://example.test/?token=excluded-secret-uri",
            UserAgent = "excluded-secret-user-agent", Resource = "https://example.test/excluded-secret-resource",
            OwnerUserId = 999, OwnerUserName = "excluded-secret-owner",
            OAuthClientDisplayName = "excluded-secret-client-name", OAuthConnectionName = "excluded-secret-connection-name"
        };
        await using (var db = CreateDbContext())
        {
            db.OAuthTokenAudits.Add(audit);
            await db.SaveChangesAsync();
        }

        using var catalogueResponse = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "audit-list", token);
        using var catalogue = await McpJsonRpcClient.ParseJsonAsync(catalogueResponse);
        using var result = await CallAsync(client, token, new { });

        var tool = McpJsonRpcClient.GetToolByName(catalogue, ToolNames.OAuthTokenAuditList);
        Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.False(tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.False(tool.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());
        var page = AssertSuccess(result);
        Assert.Equal(0, page.GetProperty("offset").GetInt32());
        Assert.Equal(100, page.GetProperty("limit").GetInt32());
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        string[] allowedFields =
        [
            "id", "occurredAtUtc", "outcome", "grantType", "requestedScopes", "errorCode",
            "presentedTokenFingerprint", "issuedRefreshTokenFingerprint", "authorizationId",
            "oauthClientId", "oauthConnectionId", "traceIdentifier"
        ];
        Assert.Equal(allowedFields.Order(), item.EnumerateObject().Select(property => property.Name).Order());
        var itemSchema = tool.GetProperty("outputSchema").GetProperty("properties").GetProperty("items").GetProperty("items");
        Assert.Equal(allowedFields.Order(), itemSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(allowedFields.Order(), itemSchema.GetProperty("required").EnumerateArray().Select(property => property.GetString()).Order());
        Assert.False(itemSchema.GetProperty("additionalProperties").GetBoolean());
        var expected = JsonSerializer.SerializeToElement(new
        {
            id = audit.Id, occurredAtUtc = Occurred, outcome = "Rejected", grantType = "refresh_token",
            requestedScopes = "mcp:read", errorCode = "invalid_grant", presentedTokenFingerprint = Fingerprint,
            issuedRefreshTokenFingerprint = (string?)null, authorizationId = "authorization-1",
            oauthClientId = "client-1", oauthConnectionId = 7, traceIdentifier = "trace-1"
        });
        Assert.True(JsonElement.DeepEquals(expected, item));
        var text = Assert.Single(result.RootElement.GetProperty("result").GetProperty("content").EnumerateArray()).GetProperty("text").GetString();
        using var textPayload = JsonDocument.Parse(text!);
        Assert.True(JsonElement.DeepEquals(page, textPayload.RootElement));
        Assert.DoesNotContain("excluded-secret", result.RootElement.GetRawText());
        Assert.False(capture.IsEnabled);
    }

    [Fact]
    public async Task AuditList_ShouldForwardExistingFiltersAndNormaliseTimestampOffsets()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        var issued = new EntityOAuthTokenAudit
        {
            OccurredAtUtc = Occurred, Outcome = "Succeeded", GrantType = "refresh_token",
            OAuthConnectionId = 7, OAuthClientId = "client-1", AuthorizationId = "authorization-1",
            IssuedRefreshTokenFingerprint = Fingerprint
        };
        var presented = new EntityOAuthTokenAudit
        {
            OccurredAtUtc = Occurred.AddMinutes(1), Outcome = "Succeeded", GrantType = "refresh_token",
            OAuthConnectionId = 7, OAuthClientId = "client-1", AuthorizationId = "authorization-1",
            PresentedTokenFingerprint = Fingerprint
        };
        await using (var db = CreateDbContext())
        {
            db.OAuthTokenAudits.AddRange(issued, presented, new EntityOAuthTokenAudit
            {
                OccurredAtUtc = Occurred.AddSeconds(30), Outcome = "Rejected", GrantType = "authorization_code",
                OAuthConnectionId = 8, OAuthClientId = "client-2", AuthorizationId = "authorization-2"
            });
            await db.SaveChangesAsync();
        }

        using var result = await CallAsync(client, token, new
        {
            offset = 1, limit = 1, fromUtc = "2026-11-03T11:00:00+02:00", toUtc = "2026-11-03T11:01:00+02:00",
            outcome = "Succeeded", grantType = "refresh_token", connectionId = 7,
            clientId = "client-1", authorizationId = "authorization-1", tokenFingerprint = Fingerprint
        });

        var page = AssertSuccess(result);
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("offset").GetInt32());
        Assert.Equal(1, page.GetProperty("limit").GetInt32());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(issued.Id, item.GetProperty("id").GetInt32());
        Assert.Equal(Fingerprint, item.GetProperty("issuedRefreshTokenFingerprint").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("presentedTokenFingerprint").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("errorCode").ValueKind);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"offset\":10,\"limit\":1}")]
    public async Task AuditList_WithoutRecordedEvents_ShouldReturnEmptyPageWithoutEnablingCapture(string arguments)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        var capture = Factory.Services.GetRequiredService<OAuthTokenAuditCaptureState>();
        Assert.False(capture.IsEnabled);

        using var result = await CallAsync(client, token, JsonSerializer.Deserialize<JsonElement>(arguments));

        var page = AssertSuccess(result);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        Assert.False(capture.IsEnabled);
    }

    [Theory]
    [InlineData("{\"limit\":201}")]
    [InlineData("{\"offset\":-1}")]
    [InlineData("{\"connectionId\":0}")]
    [InlineData("{\"outcome\":\"invalid\"}")]
    [InlineData("{\"fromUtc\":\"2026-11-04T00:00:00Z\",\"toUtc\":\"2026-11-03T00:00:00Z\"}")]
    [InlineData("{\"includeErrorDescription\":true}")]
    public async Task AuditList_WithInvalidArguments_ShouldReturnStandardValidationErrors(string arguments)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);

        using var result = await CallAsync(client, token, JsonSerializer.Deserialize<JsonElement>(arguments));

        Assert.True(result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(result);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.OAuthTokenAuditList, arguments }, "audit-call", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }
}
