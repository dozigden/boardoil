using BoardOil.Abstractions.Image;

namespace BoardOil.Services.Image;

public sealed class LocalImageStorageService(ImageStorageOptions options) : IImageStorageService
{
    private readonly string _rootPath = Path.GetFullPath(options.RootPath);

    public async Task<ImageStorageSaveResult> SaveAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Content is null || !request.Content.CanRead)
        {
            throw new ArgumentException("Image content stream must be readable.", nameof(request));
        }

        var nowUtc = DateTime.UtcNow;
        var fileName = BuildStoredFileName(request.OriginalFileName);
        var entityTypeSegment = request.EntityType.ToString().ToLowerInvariant();
        var relativePath = Path.Combine(entityTypeSegment, request.EntityId.ToString(), fileName);
        var targetPath = Path.Combine(_rootPath, relativePath);
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Failed to resolve target directory for image storage.");
        }

        Directory.CreateDirectory(targetDirectory);

        long byteLength = 0;
        await using (var output = new FileStream(
                         targetPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         useAsync: true))
        {
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await request.Content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                byteLength += bytesRead;
            }
        }

        return new ImageStorageSaveResult
        {
            RelativePath = relativePath.Replace('\\', '/'),
            ByteLength = byteLength,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var normalisedRelativePath = relativePath.Replace('\\', '/');
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalisedRelativePath));
        if (!fullPath.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static string BuildStoredFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }
        else
        {
            extension = extension.ToLowerInvariant();
        }

        return $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
    }
}
