using System.Net;
using BoardOil.Api.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
    public async Task UploadProfileImage_WhenImageIsSquare_ShouldReturnCreated_AndGetShouldReturnImageMetadata()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        using var uploadContent = new MultipartFormDataContent();
        var squareImageContent = new ByteArrayContent(CreatePngBytes(96, 96));
        squareImageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(squareImageContent, "file", "avatar.png");

        var uploadResponse = await client.PostAsync("/api/users/me/profile-image", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var readResponse = await client.GetAsync("/api/users/me/profile-image");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
    }

    [Fact]
    public async Task UploadProfileImage_WhenImageIsNotSquare_ShouldReturnBadRequest()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        using var uploadContent = new MultipartFormDataContent();
        var nonSquareImageContent = new ByteArrayContent(CreatePngBytes(96, 80));
        nonSquareImageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(nonSquareImageContent, "file", "not-square.png");

        var uploadResponse = await client.PostAsync("/api/users/me/profile-image", uploadContent);

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProfileImage_WhenImageExists_ShouldReturnOk_AndGetShouldReturnNotFound()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        using var uploadContent = new MultipartFormDataContent();
        var squareImageContent = new ByteArrayContent(CreatePngBytes(96, 96));
        squareImageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(squareImageContent, "file", "avatar.png");

        var uploadResponse = await client.PostAsync("/api/users/me/profile-image", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync("/api/users/me/profile-image");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var readResponse = await client.GetAsync("/api/users/me/profile-image");
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProfileImage_WhenImageDoesNotExist_ShouldReturnNotFound()
    {
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        var deleteResponse = await client.DeleteAsync("/api/users/me/profile-image");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProfileImage_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var client = CreateClient();

        var deleteResponse = await client.DeleteAsync("/api/users/me/profile-image");

        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
