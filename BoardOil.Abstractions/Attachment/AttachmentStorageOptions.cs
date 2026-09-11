namespace BoardOil.Abstractions.Attachment;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; init; } = string.Empty;
    public long MaxUploadByteLength { get; init; } = 10 * 1024 * 1024;
}
