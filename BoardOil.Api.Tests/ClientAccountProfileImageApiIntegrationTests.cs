using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Users;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ClientAccountProfileImageApiIntegrationTests : ApiFactoryIntegrationTestBase
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
    public async Task UploadClientAccountProfileImage_WhenImageIsSquare_ShouldReturnCreated_AndListShouldIncludeProfileImagePath()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        var clientAccountId = await CreateClientAccountAsync(adminClient, "client-with-image");

        using var uploadContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(CreatePngBytes(96, 96));
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(imageContent, "file", "avatar.png");

        // Act
        var uploadResponse = await adminClient.PostAsync($"/api/system/client-accounts/{clientAccountId}/profile-image", uploadContent);
        var listEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<ClientAccountDto>>>("/api/system/client-accounts");

        // Assert
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);

        var account = Assert.Single(listEnvelope.Data!, x => x.Id == clientAccountId);
        Assert.False(string.IsNullOrWhiteSpace(account.ProfileImageRelativePath));
    }

    [Fact]
    public async Task DeleteClientAccountProfileImage_WhenImageExists_ShouldReturnOk_AndListShouldClearProfileImagePath()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        var clientAccountId = await CreateClientAccountAsync(adminClient, "client-with-removable-image");

        using (var uploadContent = new MultipartFormDataContent())
        {
            var imageContent = new ByteArrayContent(CreatePngBytes(96, 96));
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            uploadContent.Add(imageContent, "file", "avatar.png");
            var uploadResponse = await adminClient.PostAsync($"/api/system/client-accounts/{clientAccountId}/profile-image", uploadContent);
            Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        }

        // Act
        var deleteResponse = await adminClient.DeleteAsync($"/api/system/client-accounts/{clientAccountId}/profile-image");
        var listEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<ClientAccountDto>>>("/api/system/client-accounts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);

        var account = Assert.Single(listEnvelope.Data!, x => x.Id == clientAccountId);
        Assert.Null(account.ProfileImageRelativePath);
    }

    private static async Task<int> CreateClientAccountAsync(HttpClient adminClient, string userName)
    {
        var request = new CreateClientAccountRequest(
            userName,
            userName,
            $"{userName}@localhost",
            "Standard");

        var response = await adminClient.PostAsJsonAsync("/api/system/client-accounts", request);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreatedClientAccountDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Account.Id;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
