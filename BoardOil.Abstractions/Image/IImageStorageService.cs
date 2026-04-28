namespace BoardOil.Abstractions.Image;

public interface IImageStorageService
{
    Task<ImageStorageSaveResult> SaveAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken = default);
}
