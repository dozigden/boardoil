using System.Buffers.Binary;
using BoardOil.Abstractions.Image;
using BoardOil.Contracts.Users;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task UploadOwnProfileImageAsync_WhenImageDoesNotHaveRequiredDimensions_ShouldReturnValidationError()
    {
        var service = ResolveService<IUserProfileImageService>();
        var imageBytes = CreatePngHeader(120, 80);
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

        var firstBytes = CreatePngHeader(512, 512);
        await using (var firstStream = new MemoryStream(firstBytes, writable: false))
        {
            var firstUpload = await service.UploadOwnProfileImageAsync(ActorUserId, "first.jpg", "image/png", firstStream);
            Assert.True(firstUpload.Success);
        }

        var initial = await DbContextForAssert.Images
            .SingleAsync(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId);
        var initialId = initial.Id;

        var initialRelativePath = initial.RelativePath;
        var secondBytes = CreatePngHeader(512, 512);
        await using (var secondStream = new MemoryStream(secondBytes, writable: false))
        {
            var secondUpload = await service.UploadOwnProfileImageAsync(ActorUserId, "second.html", "image/png", secondStream);
            Assert.True(secondUpload.Success);
            Assert.NotNull(secondUpload.Data);
            Assert.Equal(512, secondUpload.Data!.Width);
            Assert.Equal(512, secondUpload.Data.Height);
            Assert.EndsWith(".png", secondUpload.Data.RelativePath, StringComparison.Ordinal);
            Assert.NotEqual(initialRelativePath, secondUpload.Data.RelativePath);
        }

        DbContextForAssert.ChangeTracker.Clear();
        var rows = await DbContextForAssert.Images
            .Where(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(initialId, rows[0].Id);
        Assert.Equal(512, rows[0].Width);
        Assert.Equal(512, rows[0].Height);
        Assert.Equal("second.html", rows[0].OriginalFileName);
        Assert.Equal("image/png", rows[0].ContentType);
    }

    [Fact]
    public async Task DeleteOwnProfileImageAsync_WhenImageExists_ShouldDeleteMetadataAndFile()
    {
        var service = ResolveService<IUserProfileImageService>();

        var bytes = CreatePngHeader(512, 512);
        await using (var stream = new MemoryStream(bytes, writable: false))
        {
            var uploadResult = await service.UploadOwnProfileImageAsync(ActorUserId, "avatar.png", "image/png", stream);
            Assert.True(uploadResult.Success);
        }

        var existing = await DbContextForAssert.Images
            .SingleAsync(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId);
        var fullPath = Path.Combine(_imageRootPath, existing.RelativePath);
        Assert.True(File.Exists(fullPath));

        var result = await service.DeleteOwnProfileImageAsync(ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var remaining = await DbContextForAssert.Images
            .CountAsync(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == ActorUserId);
        Assert.Equal(0, remaining);
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteOwnProfileImageAsync_WhenImageIsMissing_ShouldReturnNotFound()
    {
        var service = ResolveService<IUserProfileImageService>();

        var result = await service.DeleteOwnProfileImageAsync(ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UploadOwnProfileImageAsync_WhenContentTypeIsNotPng_ShouldReturnValidationError()
    {
        var service = ResolveService<IUserProfileImageService>();
        await using var stream = new MemoryStream(CreatePngHeader(512, 512), writable: false);

        var result = await service.UploadOwnProfileImageAsync(
            ActorUserId,
            "avatar.jpg",
            "image/jpeg",
            stream);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UploadOwnProfileImageAsync_WhenPngHeaderIsInvalid_ShouldReturnValidationError()
    {
        var service = ResolveService<IUserProfileImageService>();
        await using var stream = new MemoryStream([0x01, 0x02, 0x03], writable: false);

        var result = await service.UploadOwnProfileImageAsync(
            ActorUserId,
            "avatar.png",
            "image/png",
            stream);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UploadOwnProfileImageAsync_WhenFileIsTooLarge_ShouldReturnValidationError()
    {
        var service = ResolveService<IUserProfileImageService>();
        var content = new byte[ProfileImageUploadConstraints.MaxByteLength + 1];
        CreatePngHeader(512, 512).CopyTo(content, 0);
        await using var stream = new MemoryStream(content, writable: false);

        var result = await service.UploadOwnProfileImageAsync(
            ActorUserId,
            "avatar.png",
            "image/png",
            stream);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        byte[] header =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0x00, 0x00, 0x00, 0x0d,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x08, 0x06, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20, 4), height);
        return header;
    }
}
