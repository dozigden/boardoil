namespace BoardOil.Services.Attachment;

internal static class AttachmentImageFileName
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".webp",
    };

    public static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName));
}
