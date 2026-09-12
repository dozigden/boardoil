using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AttachmentApiIntegrationTests : TestBaseIntegration
{
    [Fact]
    public async Task Attachments_ShouldSupportMultipartListProtectedDownloadDuplicateAndDelete()
    {
        var card = await CreateCard();
        byte[] bytes = [0, 255, 13, 10, 42];
        using var form = Upload(bytes, "original.svg");
        var upload = await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = (await upload.Content.ReadFromJsonAsync<Envelope<CardAttachmentDto>>())!.Data!;
        Assert.Equal("original.svg", attachment.OriginalFileName);
        var list = await Client.GetFromJsonAsync<Envelope<CardAttachmentListDto>>($"/api/boards/1/cards/{card.Id}/attachments");
        Assert.Equal(attachment.Id, Assert.Single(list!.Data!.Items).Id);
        Assert.Equal(10 * 1024 * 1024, list.Data.MaxUploadByteLength);

        var download = await Client.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download");
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("original.svg", download.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.True(download.Headers.CacheControl.Private);

        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download")).StatusCode);
        var duplicate = await Client.PostAsJsonAsync($"/api/boards/1/cards/{card.Id}/duplicate",
            new CreateCardRequest(null, "Duplicate", "Edited", []));
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
        var duplicateCard = (await duplicate.Content.ReadFromJsonAsync<Envelope<CardDto>>())!.Data!;
        var copied = await Client.GetFromJsonAsync<Envelope<CardAttachmentListDto>>($"/api/boards/1/cards/{duplicateCard.Id}/attachments");
        Assert.NotEqual(attachment.Id, Assert.Single(copied!.Data!.Items).Id);

        var deleted = await Client.DeleteAsync($"/api/boards/1/cards/{card.Id}/attachments/{attachment.Id}");
        Assert.True(deleted.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/api/boards/1/attachments/{attachment.Id}/download")).StatusCode);
        Assert.True((await Client.GetAsync($"/api/boards/1/attachments/{copied.Data.Items[0].Id}/download")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task MultipartUpload_ShouldRequireCsrfAndMapMissingFileValidation()
    {
        var card = await CreateCard();
        using var empty = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", empty)).StatusCode);
        Client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        using var form = Upload([1], "file.bin");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", form)).StatusCode);
    }

    [Fact]
    public async Task ImageContent_ShouldServeDetectedInlineTypeOnlyToAuthenticatedOwnerContext()
    {
        var card = await CreateCard();
        var bytes = Png(3, 2);
        using var form = Upload(bytes, "diagram.png");
        Assert.Equal(HttpStatusCode.Created,
            (await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", form)).StatusCode);
        var path = $"/api/boards/1/cards/{card.Id}/attachments/image-content?fileName=diagram.png";

        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        var otherCard = await CreateCard();
        var missingResponse = await Client.GetAsync(
            $"/api/boards/1/cards/{otherCard.Id}/attachments/image-content?fileName=diagram.png");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("nosniff", Assert.Single(missingResponse.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(missingResponse.Headers.CacheControl!.NoStore);
        Assert.True(missingResponse.Headers.CacheControl.Private);
    }

    [Fact]
    public async Task Thumbnail_ShouldSupportOptionalUploadProtectedReadAndIdempotentPut()
    {
        var card = await CreateCard();
        var thumbnail = Png(3, 2);
        using var form = Upload(Png(30, 20), "diagram.png", thumbnail);
        var upload = await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = (await upload.Content.ReadFromJsonAsync<Envelope<CardAttachmentDto>>())!.Data!;
        Assert.True(attachment.HasThumbnail);
        var path = $"/api/boards/1/attachments/{attachment.Id}/thumbnail";

        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(thumbnail, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        using var replacement = PngContent(Png(2, 2));
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsync(path, replacement)).StatusCode);
        Assert.Equal(thumbnail, await (await Client.GetAsync(path)).Content.ReadAsByteArrayAsync());
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ThumbnailPut_ShouldRequireCsrfAndValidatePngShape()
    {
        var card = await CreateCard();
        using var form = Upload(Png(3, 2), "diagram.png");
        var upload = await Client.PostAsync($"/api/boards/1/cards/{card.Id}/attachments", form);
        var attachment = (await upload.Content.ReadFromJsonAsync<Envelope<CardAttachmentDto>>())!.Data!;
        var path = $"/api/boards/1/attachments/{attachment.Id}/thumbnail";

        using var wrongType = new ByteArrayContent(Png(2, 2));
        wrongType.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Client.PutAsync(path, wrongType)).StatusCode);
        using var tooWide = PngContent(Png(201, 1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Client.PutAsync(path, tooWide)).StatusCode);
        using var oversized = PngContent(new byte[256 * 1024 + 1]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Client.PutAsync(path, oversized)).StatusCode);
        Client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        using var valid = PngContent(Png(2, 2));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PutAsync(path, valid)).StatusCode);
    }

    [Fact]
    public async Task FirstImagesByCard_ShouldReturnOneCandidatePerLiveCardAndSupportFiltering()
    {
        var firstCard = await CreateCard();
        var secondCard = await CreateCard();
        using var notes = Upload([1], "notes.txt");
        Assert.Equal(HttpStatusCode.Created,
            (await Client.PostAsync($"/api/boards/1/cards/{firstCard.Id}/attachments", notes)).StatusCode);
        using var firstImageForm = Upload(Png(3, 2), "first.png", Png(3, 2));
        var firstImageResponse = await Client.PostAsync(
            $"/api/boards/1/cards/{firstCard.Id}/attachments", firstImageForm);
        var firstImage = (await firstImageResponse.Content.ReadFromJsonAsync<Envelope<CardAttachmentDto>>())!.Data!;
        using var laterImage = Upload(Png(3, 2), "later.jpg");
        Assert.Equal(HttpStatusCode.Created,
            (await Client.PostAsync($"/api/boards/1/cards/{firstCard.Id}/attachments", laterImage)).StatusCode);
        using var secondImageForm = Upload(Png(3, 2), "second.webp");
        var secondImageResponse = await Client.PostAsync(
            $"/api/boards/1/cards/{secondCard.Id}/attachments", secondImageForm);
        var secondImage = (await secondImageResponse.Content.ReadFromJsonAsync<Envelope<CardAttachmentDto>>())!.Data!;

        var all = await Client.GetFromJsonAsync<Envelope<IReadOnlyList<CardAttachmentImageCandidateDto>>>(
            "/api/boards/1/attachment-images/first-by-card");
        var filtered = await Client.GetFromJsonAsync<Envelope<IReadOnlyList<CardAttachmentImageCandidateDto>>>(
            $"/api/boards/1/attachment-images/first-by-card?cardId={secondCard.Id}");

        Assert.Equal(2, all!.Data!.Count);
        Assert.Equal(firstImage.Id, all.Data.Single(x => x.CardId == firstCard.Id).AttachmentId);
        Assert.True(all.Data.Single(x => x.CardId == firstCard.Id).HasThumbnail);
        Assert.Equal(secondImage.Id, Assert.Single(filtered!.Data!).AttachmentId);
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/boards/1/attachment-images/first-by-card")).StatusCode);
    }

    private async Task<CardDto> CreateCard()
    {
        var response = await Client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(null, "Attachments", "", []));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<CardDto>>())!.Data!;
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string name, byte[]? thumbnail = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        if (thumbnail is not null) { form.Add(PngContent(thumbnail), "thumbnail", "thumbnail.png"); }
        return form;
    }

    private static ByteArrayContent PngContent(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return content;
    }

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(bytes, 0);
        bytes[11] = 13;
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    private sealed record Envelope<T>(bool Success, T? Data);
}
