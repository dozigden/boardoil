namespace BoardOil.Abstractions.Attachment;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; init; } = string.Empty;
    public long MaxUploadByteLength { get; init; } = 10 * 1024 * 1024;
    public long MaxImagePixelCount { get; init; } = 20_000_000;
    public int MaxImageEdgeLength { get; init; } = 10_000;
}
