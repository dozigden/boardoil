using BoardOil.Abstractions.Image;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class UserProfileImageServiceTests : TestBaseDb
{
    private readonly string _imageRootPath = Path.Combine(Path.GetTempPath(), $"boardoil-images-tests-{Guid.NewGuid():N}");

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(new ImageStorageOptions
        {
            RootPath = _imageRootPath
        });
    }

    [Fact]
    public async Task UploadOwnProfileImageAsync_WhenImageIsNotSquare_ShouldReturnValidationError()
    {
        var service = ResolveService<IUserProfileImageService>();
        var imageBytes = CreatePngBytes(120, 80);
        await using var stream = new MemoryStream(imageBytes, writable: false);

        var result = await service.UploadOwnProfileImageAsync(
            ActorUserId,
            "avatar.png",
            "image/png",
            stream);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("file", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task UploadOwnProfileImageAsync_WhenImageAlreadyExists_ShouldReplaceMetadataInPlace()
    {
        var service = ResolveService<IUserProfileImageService>();

        var firstBytes = CreatePngBytes(64, 64);
        await using (var firstStream = new MemoryStream(firstBytes, writable: false))
        {
            var firstUpload = await service.UploadOwnProfileImageAsync(ActorUserId, "first.png", "image/png", firstStream);
            Assert.True(firstUpload.Success);
        }

        var initial = await DbContextForAssert.Images
            .SingleAsync(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId);
        var initialId = initial.Id;

        var secondBytes = CreatePngBytes(96, 96);
        await using (var secondStream = new MemoryStream(secondBytes, writable: false))
        {
            var secondUpload = await service.UploadOwnProfileImageAsync(ActorUserId, "second.png", "image/png", secondStream);
            Assert.True(secondUpload.Success);
            Assert.NotNull(secondUpload.Data);
            Assert.Equal(96, secondUpload.Data!.Width);
            Assert.Equal(96, secondUpload.Data.Height);
        }

        DbContextForAssert.ChangeTracker.Clear();
        var rows = await DbContextForAssert.Images
            .Where(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(initialId, rows[0].Id);
        Assert.Equal(96, rows[0].Width);
        Assert.Equal(96, rows[0].Height);
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
