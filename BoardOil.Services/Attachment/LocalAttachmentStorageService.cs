using BoardOil.Abstractions.Attachment;

namespace BoardOil.Services.Attachment;

public sealed class LocalAttachmentStorageService(AttachmentStorageOptions options) : IAttachmentStorageService
{
    public Stream Create(string storageKey)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
    }

    public Stream OpenRead(string storageKey) =>
        new FileStream(ResolvePath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public void Delete(string storageKey)
    {
        try { File.Delete(ResolvePath(storageKey)); }
        catch (DirectoryNotFoundException) { /* The file was never created or is already gone. */ }
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new InvalidOperationException("Attachment storage has not been configured.");
        }
        if (storageKey.Length != 32 || !storageKey.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("Invalid attachment storage key.", nameof(storageKey));
        }
        return Path.Combine(Path.GetFullPath(options.RootPath), storageKey[..2], storageKey);
    }
}
