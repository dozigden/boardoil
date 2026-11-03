using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpJobIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpJobIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Jobs_WithDiagnosticOnlyToken_ShouldExposeExistingHistoryAndDetails()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);
        var occurred = new DateTime(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc);
        var older = new EntityJob { Type = "history.purge", Status = JobStatus.Completed, RunAfterUtc = occurred };
        var job = new EntityJob
        {
            Type = "resave-board", Status = JobStatus.Failed,
            RunAfterUtc = occurred, StartedAtUtc = occurred.AddSeconds(1),
            CompletedAtUtc = occurred.AddSeconds(2), UserId = 1,
            CorrelationId = "scheduled:resave-all-boards:1:20261103T030000Z",
            PayloadJson = """{"BoardId":1}""", ResultJson = "{}",
            ErrorMessage = "Board save failed.",
            Logs =
            [
                new EntityJobLog { Level = JobLogLevel.Error, Message = "Job failed.", DataJson = """{"errorMessage":"Board save failed."}""", LoggedAtUtc = occurred.AddSeconds(2) },
                new EntityJobLog { Level = JobLogLevel.Info, Message = "Job started.", LoggedAtUtc = occurred.AddSeconds(1) }
            ]
        };
        await using (var db = CreateDbContext())
        {
            db.Jobs.Add(older);
            await db.SaveChangesAsync();
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
        }

        using var catalogue = await ListToolsAsync(client, token);
        using var list = await CallAsync(client, token, ToolNames.JobList, new { });
        using var page = await CallAsync(client, token, ToolNames.JobList, new { offset = 1, limit = 1 });
        using var details = await CallAsync(client, token, ToolNames.JobGet, new { id = job.Id });
        var existingDetails = await client.GetFromJsonAsync<ApiResult<JobDetailsDto>>($"/api/system/jobs/{job.Id}");

        foreach (var name in new[] { ToolNames.JobList, ToolNames.JobGet })
        {
            var tool = McpJsonRpcClient.GetToolByName(catalogue, name);
            Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
            Assert.False(tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
            Assert.False(tool.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());
            Assert.Equal("object", tool.GetProperty("outputSchema").GetProperty("type").GetString());
        }
        Assert.DoesNotContain(ToolNames.BoardList, ToolNamesFrom(catalogue));
        var listed = AssertSuccess(list);
        Assert.Equal(0, listed.GetProperty("offset").GetInt32());
        Assert.Equal(100, listed.GetProperty("limit").GetInt32());
        var newest = listed.GetProperty("items")[0];
        Assert.Equal(job.Id, newest.GetProperty("id").GetInt32());
        Assert.Equal(job.PayloadJson, newest.GetProperty("payloadJson").GetString());
        Assert.False(newest.TryGetProperty("logs", out _));
        var paged = AssertSuccess(page);
        Assert.Equal(older.Id, Assert.Single(paged.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(1, paged.GetProperty("offset").GetInt32());
        Assert.Equal(1, paged.GetProperty("limit").GetInt32());
        Assert.Equal(listed.GetProperty("totalCount").GetInt32(), paged.GetProperty("totalCount").GetInt32());
        var detail = AssertSuccess(details);
        Assert.True(existingDetails!.Success);
        Assert.True(JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(existingDetails.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web)), detail));
        Assert.Equal("failed", detail.GetProperty("status").GetString());
        Assert.Equal("Job started.", detail.GetProperty("logs")[0].GetProperty("message").GetString());
        Assert.Equal("Job failed.", detail.GetProperty("logs")[1].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(ToolNames.JobList, """{"limit":201}""", "validation_failed", 400)]
    [InlineData(ToolNames.JobList, """{"offset":-1}""", "validation_failed", 400)]
    [InlineData(ToolNames.JobGet, "{}", "validation_failed", 400)]
    [InlineData(ToolNames.JobGet, """{"id":0}""", "validation_failed", 400)]
    [InlineData(ToolNames.JobGet, """{"id":2147483647}""", "not_found", 404)]
    public async Task Jobs_WithInvalidArgumentsOrMissingRecord_ShouldReturnStandardErrors(
        string name, string argumentsJson, string code, int statusCode)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:diagnostics:read"]);

        using var result = await CallAsync(client, token, name, JsonSerializer.Deserialize<JsonElement>(argumentsJson));

        AssertError(result, code, statusCode);
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
