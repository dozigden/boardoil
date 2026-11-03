using System.Net;
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

public sealed class McpSystemInfoIntegrationTests : McpIntegrationTestBase
{
    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(databasePath, configurationOverrides: new Dictionary<string, string?>
        {
            ["BoardOilBuild:Version"] = "9.8.7",
            ["BoardOilBuild:Channel"] = "nightly",
            ["BoardOilBuild:Build"] = "build-123",
            ["BoardOilBuild:Commit"] = "abc123"
        });

    [Theory]
    [InlineData("mcp:read")]
    [InlineData("mcp:write")]
    [InlineData("mcp:diagnostics:read")]
    public async Task SystemInfo_WithStandardUserAndAnyMcpScope_ShouldReturnBuildAndDatabaseMetadata(string scope)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [scope]);
        await using (var db = Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>())
        {
            var user = await db.Users.SingleAsync(user => user.UserName == "admin");
            user.Role = UserRole.Standard;
            await db.SaveChangesAsync();
        }

        using var discoveryResponse = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "system-info-list", token);
        using var discovery = await McpJsonRpcClient.ParseJsonAsync(discoveryResponse);
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.SystemInfoGet, arguments = new { } }, "system-info-call", token);
        using var result = await McpJsonRpcClient.ParseJsonAsync(response);
        using var versionResponse = await client.GetAsync("/api/version");
        using var version = JsonDocument.Parse(await versionResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, discoveryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var tool = McpJsonRpcClient.GetToolByName(discovery, ToolNames.SystemInfoGet);
        Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.False(tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.Empty(tool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject());
        Assert.False(tool.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());

        var info = McpJsonRpcClient.GetStructuredContent(result);
        Assert.Equal(["application", "database"], info.EnumerateObject().Select(property => property.Name).Order());
        var application = info.GetProperty("application");
        Assert.True(JsonElement.DeepEquals(version.RootElement.GetProperty("data"), application));
        Assert.Equal("9.8.7", application.GetProperty("version").GetString());
        Assert.Equal("nightly", application.GetProperty("channel").GetString());
        Assert.Equal("build-123", application.GetProperty("build").GetString());
        Assert.Equal("abc123", application.GetProperty("commit").GetString());
        var database = info.GetProperty("database");
        Assert.Equal(["journalMode", "provider", "sizeBytes", "version"], database.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("SQLite", database.GetProperty("provider").GetString());
        Assert.NotEmpty(database.GetProperty("version").GetString()!);
        Assert.Equal("wal", database.GetProperty("journalMode").GetString());
        Assert.True(database.GetProperty("sizeBytes").GetInt64() > 0);
        foreach (var section in new[] { "application", "database" })
        {
            var schema = tool.GetProperty("outputSchema").GetProperty("properties").GetProperty(section);
            Assert.Equal(info.GetProperty(section).EnumerateObject().Select(property => property.Name).Order(),
                schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public async Task SystemInfo_WithoutMcpCredentials_ShouldRequireAuthentication()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.SystemInfoGet, arguments = new { } }, "system-info-unauthorised");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SystemInfo_WithUnexpectedArguments_ShouldReturnValidationError()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.SystemInfoGet, arguments = new { includeConfiguration = true } }, "system-info-invalid", token);
        using var result = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(result);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
    }
}
