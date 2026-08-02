using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Users;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class UserProfileImageApiIntegrationTests : ApiFactoryIntegrationTestBase
{
    protected override BoardOilApiFactory CreateFactory(string databasePath)
    {
        var imageRootPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "images");
        return new BoardOilApiFactory(
            databasePath,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["BoardOil:ImageRootPath"] = imageRootPath
            });
    }

    [Fact]
    public async Task UploadProfileImage_WhenPngMatchesContract_ShouldStoreCanonicalPng_AndGetShouldReturnImageMetadata()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        using var uploadContent = new MultipartFormDataContent();
        var squareImageContent = new ByteArrayContent(CreatePngHeader(512, 512));
        squareImageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(squareImageContent, "file", "avatar.html");

        var uploadResponse = await client.PostAsync("/api/users/me/profile-image", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploadEnvelope = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserProfileImageDto>>();
        Assert.NotNull(uploadEnvelope?.Data);
        Assert.Equal(512, uploadEnvelope.Data.Width);
        Assert.Equal(512, uploadEnvelope.Data.Height);
        Assert.EndsWith(".png", uploadEnvelope.Data.RelativePath, StringComparison.Ordinal);

        var storedImageResponse = await client.GetAsync($"/images/{uploadEnvelope.Data.RelativePath}");
        Assert.Equal(HttpStatusCode.OK, storedImageResponse.StatusCode);
        Assert.Equal("image/png", storedImageResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("nosniff", storedImageResponse.Headers.GetValues("X-Content-Type-Options"));

        var readResponse = await client.GetAsync("/api/users/me/profile-image");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
    }

    [Fact]
    public async Task UploadProfileImage_WhenFileExceedsLimit_ShouldReturnBadRequest()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);
        var content = new byte[ProfileImageUploadConstraints.MaxByteLength + 1];
        CreatePngHeader(512, 512).CopyTo(content, 0);
        using var uploadContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(content);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(imageContent, "file", "avatar.png");

        var uploadResponse = await client.PostAsync("/api/users/me/profile-image", uploadContent);

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    private static byte[] CreatePngHeader(int width, int height) => PngHeaderTestData.Create(width, height);

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
