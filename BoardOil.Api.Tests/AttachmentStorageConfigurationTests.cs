using BoardOil.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AttachmentStorageConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boardoil-storage-config-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_ShouldDefaultToPrivateDirectoryAlongsideDatabase()
    {
        var result = Resolve(new Dictionary<string, string?>());
        Assert.Equal(Path.Combine(_root, "attachments"), result.RootPath);
        Assert.Equal(10 * 1024 * 1024, result.MaxUploadByteLength);
    }

    [Theory]
    [InlineData("images")]
    [InlineData("wwwroot")]
    public void Resolve_ShouldRejectPublicStorage(string directory)
    {
        Assert.Throws<InvalidOperationException>(() => Resolve(new Dictionary<string, string?>
        {
            ["BoardOil:AttachmentRootPath"] = Path.Combine(_root, directory, "attachments")
        }));
    }

    [Fact]
    public void Resolve_ShouldRejectSymlinkIntoPublicStorage()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        Directory.CreateSymbolicLink(Path.Combine(_root, "private-link"), Path.Combine(_root, "images"));
        Assert.Throws<InvalidOperationException>(() => Resolve(new Dictionary<string, string?>
        {
            ["BoardOil:AttachmentRootPath"] = Path.Combine(_root, "private-link", "attachments")
        }));
    }

    private BoardOil.Abstractions.Attachment.AttachmentStorageOptions Resolve(Dictionary<string, string?> values) =>
        BoardOilAttachmentStorageOptions.Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            "Data Source=" + Path.Combine(_root, "boardoil.db"), Path.Combine(_root, "images"), Path.Combine(_root, "wwwroot"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }
}
