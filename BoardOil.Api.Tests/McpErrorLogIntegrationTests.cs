using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpErrorLogIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpErrorLogIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData("mcp:read", true)]
    [InlineData("mcp:write", true)]
    [InlineData("mcp:diagnostics:read", false)]
    public async Task Diagnostics_WithoutExplicitPermissionOrAdmin_ShouldBeHiddenAndUncallable(string permission, bool admin)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [permission]);
        if (!admin)
        {
            await using var db = CreateDbContext();
            var user = await db.Users.SingleAsync(x => x.UserName == "admin");
            user.Role = UserRole.Standard;
            await db.SaveChangesAsync();
        }

        using var list = await ListToolsAsync(client, token);
        using var search = await CallAsync(client, token, ToolNames.ErrorLogList, new { });
        using var details = await CallAsync(client, token, ToolNames.ErrorLogGet, new { id = 1 });

        Assert.DoesNotContain(ToolNames.ErrorLogList, ToolNamesFrom(list));
        Assert.DoesNotContain(ToolNames.ErrorLogGet, ToolNamesFrom(list));
        Assert.Equal(permission != "mcp:diagnostics:read", ToolNamesFrom(list).Contains(ToolNames.BoardList));
        AssertError(search, "forbidden", 403);
        AssertError(details, "forbidden", 403);
    }

    [Fact]
    public async Task Diagnostics_WithAdminDiagnosticOnlyToken_ShouldAdvertiseReadOnlySchemasAndReturnSanitisedDetails()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        var occurred = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var error = new EntityErrorLog
        {
            OccurredAtUtc = occurred, Source = "Backend", Area = "JobRunner", ExceptionType = "TestException",
            Message = "Queue failure password=private-message Cookie: theme=dark; boardoil_refresh=private-cookie",
            StackTrace = "trace Bearer private-stack\nCookie: theme=dark; boardoil_refresh=private-stack-cookie\nat Worker",
            ContextJson = """{"nested":{"client_secret":"private-context","reason":"queue unavailable","headers":"Cookie: theme=dark; boardoil_refresh=private-context-cookie"}}""",
            TraceIdentifier = "trace-diagnostic", RequestMethod = "POST", RequestPath = "/api/test?access_token=private-query",
            ActorUserId = 1,
            Job = new EntityJob { Type = "sample", Status = JobStatus.Failed, RunAfterUtc = occurred }
        };
        await using (var db = CreateDbContext())
        {
            db.ErrorLogs.Add(error);
            await db.SaveChangesAsync();
        }

        using var catalogue = await ListToolsAsync(client, token);
        using var search = await CallAsync(client, token, ToolNames.ErrorLogList, new
        {
            offset = 0, limit = 1, fromUtc = occurred, toUtc = occurred.AddMinutes(1),
            source = "Backend", area = "JobRunner", message = "QUEUE", traceIdentifier = "trace-diagnostic"
        });
        using var details = await CallAsync(client, token, ToolNames.ErrorLogGet, new { id = error.Id });

        foreach (var name in new[] { ToolNames.ErrorLogList, ToolNames.ErrorLogGet })
        {
            var tool = McpJsonRpcClient.GetToolByName(catalogue, name);
            var annotations = tool.GetProperty("annotations");
            Assert.True(annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.False(annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.False(tool.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());
            Assert.Equal("object", tool.GetProperty("outputSchema").GetProperty("type").GetString());
        }
        Assert.DoesNotContain(ToolNames.BoardList, ToolNamesFrom(catalogue));
        var listed = AssertSuccess(search);
        Assert.Equal(1, listed.GetProperty("totalCount").GetInt32());
        Assert.Equal(error.Id, Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        var detail = AssertSuccess(details);
        Assert.Equal(error.JobId, detail.GetProperty("jobId").GetInt32());
        Assert.Equal(1, detail.GetProperty("actorUserId").GetInt32());
        Assert.Equal("trace-diagnostic", detail.GetProperty("traceIdentifier").GetString());
        Assert.Contains("queue unavailable", detail.GetProperty("contextJson").GetString()!);
        Assert.Contains("at Worker", detail.GetProperty("stackTrace").GetString()!);
        Assert.DoesNotContain("private", listed.GetRawText());
        Assert.DoesNotContain("private", detail.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Diagnostics_AfterPermissionRemovalOrAdminDemotion_ShouldRefreshListAndRejectCachedCalls(bool demote)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read", "mcp:diagnostics:read"]);
        using var before = await ListToolsAsync(client, token);
        Assert.Contains(ToolNames.ErrorLogList, ToolNamesFrom(before));
        if (demote)
        {
            await using var db = CreateDbContext();
            var user = await db.Users.SingleAsync(x => x.UserName == "admin");
            user.Role = UserRole.Standard;
            await db.SaveChangesAsync();
        }
        else
        {
            await using var db = CreateDbContext();
            var pat = await db.PersonalAccessTokens.SingleAsync();
            pat.ScopesCsv = "mcp:read";
            await db.SaveChangesAsync();
        }

        using var after = await ListToolsAsync(client, token);
        using var call = await CallAsync(client, token, ToolNames.ErrorLogList, new { });

        Assert.DoesNotContain(ToolNames.ErrorLogList, ToolNamesFrom(after));
        AssertError(call, "forbidden", 403);
        Assert.Equal("private", after.RootElement.GetProperty("result").GetProperty("cacheScope").GetString());
        Assert.Equal(0, after.RootElement.GetProperty("result").GetProperty("ttlMs").GetInt32());
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("noMcpScope")]
    [InlineData("inactive")]
    [InlineData("revoked")]
    public async Task Diagnostics_WithoutValidMcpAuthentication_ShouldRejectAtTransport(string restriction)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        string? token = null;
        if (restriction != "anonymous")
        {
            token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        }
        if (restriction == "noMcpScope")
        {
            await using var db = CreateDbContext();
            var pat = await db.PersonalAccessTokens.SingleAsync();
            pat.ScopesCsv = "api:read";
            await db.SaveChangesAsync();
        }
        if (restriction == "revoked")
        {
            await using var db = CreateDbContext();
            var pat = await db.PersonalAccessTokens.SingleAsync();
            pat.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        if (restriction == "inactive")
        {
            await using var db = CreateDbContext();
            var user = await db.Users.SingleAsync(x => x.UserName == "admin");
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        using var discovery = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "unauthorised-list", token);
        using var call = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.ErrorLogList, arguments = new { } }, "unauthorised-call", token);

        Assert.Equal(HttpStatusCode.Unauthorized, discovery.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }

    [Fact]
    public async Task ErrorLogs_WithInvalidArgumentsOrMissingId_ShouldReturnStandardErrors()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);

        using var invalid = await CallAsync(client, token, ToolNames.ErrorLogList, new { limit = 201 });
        using var missingId = await CallAsync(client, token, ToolNames.ErrorLogGet, new { });
        using var missingRecord = await CallAsync(client, token, ToolNames.ErrorLogGet, new { id = int.MaxValue });

        AssertError(invalid, "validation_failed", 400);
        AssertError(missingId, "validation_failed", 400);
        AssertError(missingRecord, "not_found", 404);
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private static IEnumerable<string?> ToolNamesFrom(JsonDocument payload) =>
        payload.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString());

    private static async Task<JsonDocument> ListToolsAsync(HttpClient client, string token)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "diagnostics-list", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string name, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "diagnostics-call", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }

    private static void AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(statusCode, error.GetProperty("statusCode").GetInt32());
    }
}
