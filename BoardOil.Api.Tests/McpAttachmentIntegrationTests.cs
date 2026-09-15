using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpAttachmentIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpAttachmentIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_ShouldReturnLiveOrArchivedMetadataAndLimit(bool archived)
    {
        // Arrange
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();
        if (archived)
        {
            var archive = await client.PostAsync($"/api/boards/1/cards/{card.Id}/archive", null);
            archive.EnsureSuccessStatusCode();
        }

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentList,
            new { boardId = 1, cardId = card.Id, archived });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(10 * 1024 * 1024, output.GetProperty("maxUploadByteLength").GetInt64());
        var item = Assert.Single(output.GetProperty("items").EnumerateArray());
        AssertMetadata(attachment, item);
    }

    [Fact]
    public async Task CardGet_ShouldIncludeAttachmentMetadataWithoutFileContents()
    {
        // Arrange
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });

        // Assert
        var output = AssertSuccess(payload);
        AssertMetadata(attachment, Assert.Single(output.GetProperty("attachments").EnumerateArray()));
    }

    [Fact]
    public async Task CardGet_WhenNoAttachments_ShouldReturnEmptyArray()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });

        // Assert
        Assert.Empty(AssertSuccess(payload).GetProperty("attachments").EnumerateArray());
    }

    [Fact]
    public async Task Delete_ShouldDeleteOnlyTheSelectedAttachment()
    {
        // Arrange
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();
        var retained = await UploadAsync(client, card.Id, "retained.bin");

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDelete,
            new { boardId = 1, cardId = card.Id, id = attachment.Id });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(attachment.Id, output.GetProperty("id").GetInt32());
        Assert.Equal("deleted", output.GetProperty("outcome").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download")).StatusCode);
        var remaining = await client.GetFromJsonAsync<ApiEnvelope<CardAttachmentListDto>>($"/api/boards/1/cards/{card.Id}/attachments");
        Assert.Equal(retained.Id, Assert.Single(remaining!.Data!.Items).Id);
    }

    [Fact]
    public async Task Delete_WhenArchived_ShouldReturnNotFoundAndRetainFile()
    {
        // Arrange
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();
        (await client.PostAsync($"/api/boards/1/cards/{card.Id}/archive", null)).EnsureSuccessStatusCode();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDelete,
            new { boardId = 1, cardId = card.Id, id = attachment.Id });

        // Assert
        AssertError(payload, "not_found", 404);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download")).StatusCode);
    }

    [Fact]
    public async Task Delete_WhenAttachmentBelongsToAnotherCard_ShouldReturnNotFound()
    {
        // Arrange
        var (client, token, _, attachment) = await ArrangeAttachmentAsync();
        var otherCard = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDelete,
            new { boardId = 1, cardId = otherCard.Id, id = attachment.Id });

        // Assert
        AssertError(payload, "not_found", 404);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download")).StatusCode);
    }

    [Theory]
    [InlineData(ToolNames.CardAttachmentUpload, "mcp:read")]
    [InlineData(ToolNames.CardAttachmentDelete, "mcp:read")]
    public async Task AttachmentTools_WithInsufficientPatScope_ShouldReturnForbidden(string toolName, string grantedScope)
    {
        // Arrange
        var (client, _, card, attachment) = await ArrangeAttachmentAsync();
        var token = await CreateMachinePatAsync(client, [grantedScope]);

        // Act
        using var payload = await CallAsync(client, token, toolName, Arguments(toolName, card.Id, attachment.Id));

        // Assert
        AssertError(payload, "forbidden", 403);
    }

    [Theory]
    [InlineData(ToolNames.CardAttachmentList)]
    [InlineData(ToolNames.CardAttachmentDownload)]
    public async Task AttachmentReadTools_WithWritePatScope_ShouldSucceed(string toolName)
    {
        // Arrange
        var (client, _, card, attachment) = await ArrangeAttachmentAsync();
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);

        // Act
        using var payload = await CallAsync(client, token, toolName, Arguments(toolName, card.Id, attachment.Id));

        // Assert
        _ = AssertSuccess(payload);
    }

    [Theory]
    [InlineData(ToolNames.CardAttachmentList)]
    [InlineData(ToolNames.CardAttachmentDownload)]
    [InlineData(ToolNames.CardAttachmentUpload)]
    [InlineData(ToolNames.CardAttachmentDelete)]
    [InlineData(ToolNames.CardGet)]
    public async Task AttachmentTools_AfterBoardMembershipRemoval_ShouldReturnForbidden(string toolName)
    {
        // Arrange
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();
        using (var services = Factory.Services.CreateScope())
        {
            var factory = services.ServiceProvider.GetRequiredService<IDbContextFactory>();
            await using var db = factory.CreateDbContext<BoardOilDbContext>();
            var membership = await db.BoardMembers.SingleAsync(x => x.BoardId == 1 && x.UserId == 1);
            db.BoardMembers.Remove(membership);
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token, toolName, Arguments(toolName, card.Id, attachment.Id));

        // Assert
        AssertError(payload, "forbidden", 403);
    }

    [Theory]
    [InlineData(ToolNames.CardAttachmentList, "{\"boardId\":1}")]
    [InlineData(ToolNames.CardAttachmentList, "{\"boardId\":1,\"cardId\":0}")]
    [InlineData(ToolNames.CardAttachmentList, "{\"boardId\":1,\"cardId\":1,\"archived\":\"yes\"}")]
    [InlineData(ToolNames.CardAttachmentDelete, "{\"boardId\":1,\"cardId\":1}")]
    [InlineData(ToolNames.CardAttachmentDownload, "{\"boardId\":1}")]
    [InlineData(ToolNames.CardAttachmentUpload, "{\"boardId\":1,\"cardId\":1,\"fileName\":\"file.bin\"}")]
    [InlineData(ToolNames.CardAttachmentUpload, "{\"boardId\":1,\"cardId\":1,\"fileName\":\"file.bin\",\"byteLength\":-1}")]
    [InlineData(ToolNames.CardAttachmentDelete, "{\"boardId\":1,\"cardId\":1,\"id\":1,\"archived\":true}")]
    public async Task AttachmentTools_WithInvalidArguments_ShouldReturnValidationError(string toolName, string arguments)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var input = JsonDocument.Parse(arguments);

        // Act
        using var payload = await CallAsync(client, token, toolName, input.RootElement);

        // Assert
        AssertError(payload, "validation_failed", 400);
    }

    private async Task<(HttpClient Client, string Token, CardDto Card, CardAttachmentDto Attachment)> ArrangeAttachmentAsync()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var card = await CreateCardAsync(client);
        var attachment = await UploadAsync(client, card.Id, "original.bin");
        return (client, token, card, attachment);
    }

    [Fact]
    public async Task DownloadTicket_ShouldStreamExactBytesAndProtectedHeadersToIndependentHttpClientConcurrently()
    {
        var (client, token, _, attachment) = await ArrangeAttachmentAsync();
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDownload, new { boardId = 1, id = attachment.Id });
        var ticket = AssertSuccess(payload);
        var url = ticket.GetProperty("url").GetString()!;
        var header = ticket.GetProperty("headers").GetProperty("Authorization").GetString()!;
        var parsedHeader = AuthenticationHeaderValue.Parse(header);
        Assert.Equal("GET", ticket.GetProperty("method").GetString());
        Assert.Equal("BoardOilAttachment", parsedHeader.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(parsedHeader.Parameter));
        Assert.Empty(new Uri(url).Query);
        var downloader = CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(header);
            return await downloader.SendAsync(request);
        }));

        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(new byte[] { 0, 255, 13, 10, 42 }, await response.Content.ReadAsByteArrayAsync());
                Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
                Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
                Assert.Contains("original.bin", response.Content.Headers.ContentDisposition.ToString());
                Assert.True(response.Headers.CacheControl!.NoStore);
                Assert.True(response.Headers.CacheControl.Private);
                Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            }
        }

        using var scope = Factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
        Assert.Equal(3, await db.AttachmentTransferAudits.CountAsync(
            x => x.Outcome == AttachmentTransferAuditOutcome.DownloadAdmitted));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("deleted")]
    [InlineData("expired")]
    [InlineData("disabled-user")]
    [InlineData("scope-removed")]
    public async Task DownloadTicket_WhenOriginatingPatInvalidated_ShouldReturnUnauthorized(string change)
    {
        var (client, token, _, attachment) = await ArrangeAttachmentAsync();
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDownload, new { boardId = 1, id = attachment.Id });
        var ticket = AssertSuccess(payload);
        using (var scope = Factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var pat = await db.PersonalAccessTokens.SingleAsync();
            switch (change)
            {
                case "revoked": pat.RevokedAtUtc = DateTime.UtcNow; break;
                case "deleted": db.PersonalAccessTokens.Remove(pat); break;
                case "expired": pat.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); break;
                case "disabled-user": (await db.Users.SingleAsync(x => x.Id == pat.UserId)).IsActive = false; break;
                case "scope-removed": pat.ScopesCsv = MachinePatScopes.ApiRead; break;
            }
            await db.SaveChangesAsync();
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, ticket.GetProperty("url").GetString());
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(ticket.GetProperty("headers").GetProperty("Authorization").GetString()!);

        using var response = await CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("cookie")]
    [InlineData("pat")]
    [InlineData("query")]
    [InlineData("bearer-ticket")]
    public async Task DownloadTicket_ShouldNotAcceptOtherAuthenticationOrQuerySecrets(string alternative)
    {
        var (client, token, _, attachment) = await ArrangeAttachmentAsync();
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDownload, new { boardId = 1, id = attachment.Id });
        var ticket = AssertSuccess(payload);
        var url = ticket.GetProperty("url").GetString()!;
        var ticketHeader = AuthenticationHeaderValue.Parse(
            ticket.GetProperty("headers").GetProperty("Authorization").GetString()!);
        if (alternative == "query")
        {
            url += "?access_token=" + ticketHeader.Parameter;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (alternative == "pat") { request.Headers.Authorization = new("Bearer", token); }
        if (alternative == "bearer-ticket") { request.Headers.Authorization = new("Bearer", ticketHeader.Parameter); }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadTicket_ShouldNotAuthenticateGeneralApiOrMcp()
    {
        var (client, token, _, attachment) = await ArrangeAttachmentAsync();
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentDownload, new { boardId = 1, id = attachment.Id });
        var header = AssertSuccess(payload).GetProperty("headers").GetProperty("Authorization").GetString()!;
        var ticketCredential = AuthenticationHeaderValue.Parse(header).Parameter!;
        var downloader = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/boards");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(header);

        using var apiResponse = await downloader.SendAsync(request);
        using var mcpResponse = await McpJsonRpcClient.SendRequestAsync(downloader, "tools/list", new { }, "wrong-credential", ticketCredential);

        Assert.Equal(HttpStatusCode.Unauthorized, apiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mcpResponse.StatusCode);
    }

    [Fact]
    public async Task UploadTicket_ShouldPublishRawBytesAndReturnExistingAttachmentOnCompletedRetry()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var card = await CreateCardAsync(client);
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentUpload,
            new { boardId = 1, cardId = card.Id, fileName = "folder/Architecture (final) #1.png", contentType = "application/x-agent", byteLength = 5 });
        var ticket = AssertSuccess(payload);
        Assert.Equal("PUT", ticket.GetProperty("method").GetString());
        Assert.Equal(5, ticket.GetProperty("byteLength").GetInt64());
        Assert.Equal("![Image](boardoil-attachment:Architecture%20%28final%29%20%231.png)",
            ticket.GetProperty("markdownSnippet").GetString());
        var headers = ticket.GetProperty("headers");
        var authorization = AuthenticationHeaderValue.Parse(headers.GetProperty("Authorization").GetString()!);
        Assert.Equal("BoardOilAttachment", authorization.Scheme);
        Assert.Equal("application/x-agent", headers.GetProperty("Content-Type").GetString());

        using var first = CreateUploadRequest(ticket, [0, 255, 13, 10, 42]);
        using var firstResponse = await CreateClient().SendAsync(first);
        var created = (await firstResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardAttachmentDto>>())!.Data!;
        using var retry = CreateUploadRequest(ticket, [9, 9, 9, 9, 9]);
        using var retryResponse = await CreateClient().SendAsync(retry);
        var repeated = (await retryResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardAttachmentDto>>())!.Data!;

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(created.Id, repeated.Id);
        Assert.Equal("Architecture (final) #1.png", created.OriginalFileName);
        using var download = await client.GetAsync($"/api/boards/1/attachments/{created.Id}/download");
        Assert.Equal(new byte[] { 0, 255, 13, 10, 42 }, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadTicket_WhenRequestMetadataIsWrong_ShouldNotConsumeTicket()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var card = await CreateCardAsync(client);
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentUpload,
            new { boardId = 1, cardId = card.Id, fileName = "metadata.bin", byteLength = 3 });
        var ticket = AssertSuccess(payload);
        using var wrong = CreateUploadRequest(ticket, [1, 2]);
        using var wrongResponse = await CreateClient().SendAsync(wrong);
        using var correct = CreateUploadRequest(ticket, [1, 2, 3]);
        using var correctResponse = await CreateClient().SendAsync(correct);

        Assert.Equal(HttpStatusCode.BadRequest, wrongResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, correctResponse.StatusCode);
    }

    [Fact]
    public async Task UploadTicket_WhenOriginatingPatLosesWriteScope_ShouldReturnUnauthorized()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var card = await CreateCardAsync(client);
        using var payload = await CallAsync(client, token, ToolNames.CardAttachmentUpload,
            new { boardId = 1, cardId = card.Id, fileName = "scope.bin", byteLength = 3 });
        var ticket = AssertSuccess(payload);
        using (var scope = Factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var pat = await db.PersonalAccessTokens.SingleAsync();
            pat.ScopesCsv = MachinePatScopes.McpRead;
            await db.SaveChangesAsync();
        }
        using var request = CreateUploadRequest(ticket, [1, 2, 3]);

        using var response = await CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TransferTickets_ShouldUseConfiguredPublicBaseUrlIncludingPath()
    {
        var (client, token, card, attachment) = await ArrangeAttachmentAsync();
        const string publicBaseUrl = "https://gateway.example.com/boardoil";
        using var configuration = await client.PutAsJsonAsync(
            "/api/system/configuration", new UpdateConfigurationRequest(publicBaseUrl, false));
        configuration.EnsureSuccessStatusCode();

        using var downloadPayload = await CallAsync(client, token, ToolNames.CardAttachmentDownload,
            new { boardId = 1, id = attachment.Id });
        using var uploadPayload = await CallAsync(client, token, ToolNames.CardAttachmentUpload,
            new { boardId = 1, cardId = card.Id, fileName = "public.bin", byteLength = 3 });
        var download = AssertSuccess(downloadPayload);
        var upload = AssertSuccess(uploadPayload);

        Assert.StartsWith(
            $"{publicBaseUrl}/api/attachment-transfers/", download.GetProperty("url").GetString());
        Assert.EndsWith("/download", download.GetProperty("url").GetString());
        Assert.StartsWith(
            $"{publicBaseUrl}/api/attachment-transfers/", upload.GetProperty("url").GetString());
        Assert.EndsWith("/upload", upload.GetProperty("url").GetString());
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(null, "MCP attachments", "", []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task<CardAttachmentDto> UploadAsync(HttpClient client, int cardId, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0, 255, 13, 10, 42]), "file", fileName);
        using var response = await client.PostAsync($"/api/boards/1/cards/{cardId}/attachments", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardAttachmentDto>>())!.Data!;
    }

    private static object Arguments(string toolName, int cardId, int attachmentId) => toolName switch
    {
        ToolNames.CardAttachmentDelete => new { boardId = 1, cardId, id = attachmentId },
        ToolNames.CardAttachmentDownload => new { boardId = 1, id = attachmentId },
        ToolNames.CardAttachmentUpload => new { boardId = 1, cardId, fileName = "agent.bin", byteLength = 3 },
        ToolNames.CardGet => new { boardId = 1, id = cardId },
        _ => new { boardId = 1, cardId }
    };

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string toolName, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = toolName, arguments }, "attachment-test", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private static HttpRequestMessage CreateUploadRequest(JsonElement ticket, byte[] bytes)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, ticket.GetProperty("url").GetString());
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(
            ticket.GetProperty("headers").GetProperty("Authorization").GetString()!);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            ticket.GetProperty("headers").GetProperty("Content-Type").GetString()!);
        return request;
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        var result = payload.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var output = result.GetProperty("structuredContent");
        var text = Assert.Single(result.GetProperty("content").EnumerateArray());
        Assert.Equal("text", text.GetProperty("type").GetString());
        using var textPayload = JsonDocument.Parse(text.GetProperty("text").GetString()!);
        Assert.True(JsonElement.DeepEquals(output, textPayload.RootElement));
        return output;
    }

    private static void AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, output.GetProperty("code").GetString());
        Assert.Equal(statusCode, output.GetProperty("statusCode").GetInt32());
        var text = Assert.Single(payload.RootElement.GetProperty("result").GetProperty("content").EnumerateArray());
        using var textPayload = JsonDocument.Parse(text.GetProperty("text").GetString()!);
        Assert.True(JsonElement.DeepEquals(output, textPayload.RootElement));
    }

    private static void AssertMetadata(CardAttachmentDto attachment, JsonElement item)
    {
        Assert.Equal(attachment.Id, item.GetProperty("id").GetInt32());
        Assert.Equal(attachment.OriginalFileName, item.GetProperty("originalFileName").GetString());
        Assert.Equal(attachment.ByteLength, item.GetProperty("byteLength").GetInt64());
        Assert.Equal(attachment.ContentType, item.GetProperty("contentType").GetString());
        Assert.Equal(attachment.CreatedAtUtc, item.GetProperty("createdAtUtc").GetDateTime());
        Assert.Equal(attachment.CreatedByUserId, item.GetProperty("createdByUserId").GetInt32());
        Assert.Equal(attachment.HasThumbnail, item.GetProperty("hasThumbnail").GetBoolean());
        Assert.Equal(7, item.EnumerateObject().Count());
    }
}
