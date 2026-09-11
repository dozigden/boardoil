using System.Net;
using BoardOil.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AttachmentTransferHttpsIntegrationTests : McpIntegrationTestBase
{
    protected override BoardOilApiFactory CreateFactory(string databasePath) => new(databasePath, allowInsecureCookies: false);

    [Fact]
    public async Task DownloadEndpoint_WhenHttpWithoutDevelopmentOptOut_ShouldRejectBeforeAuthentication()
    {
        using var response = await CreateClient().GetAsync("/api/attachment-transfers/1/download");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HTTPS", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadEndpoint_WhenHttpWithoutDevelopmentOptOut_ShouldRejectBeforeAuthentication()
    {
        using var response = await CreateClient().PutAsync("/api/attachment-transfers/1/upload", new ByteArrayContent([1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HTTPS", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TicketIssuance_WhenHttpWithoutDevelopmentOptOut_ShouldReject()
    {
        var secureClient = TrackClient(Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }));
        await RegisterInitialAdminAsync(secureClient);
        var token = await CreateMachinePatAsync(secureClient);

        using var response = await McpJsonRpcClient.SendRequestAsync(CreateClient(), "tools/call",
            new { name = "card_attachment_download", arguments = new { boardId = 1, id = 1 } }, "http-ticket", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal(400, McpJsonRpcClient.GetStructuredContent(payload).GetProperty("statusCode").GetInt32());
        Assert.Contains("HTTPS", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("message").GetString());
    }
}
