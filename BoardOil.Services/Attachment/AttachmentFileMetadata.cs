using System.Net.Http.Headers;

namespace BoardOil.Services.Attachment;

public static class AttachmentFileMetadata
{
    public static string FileName(string value)
    {
        if (value is null) { throw new ArgumentException("File name is required."); }
        var name = value.Replace('\\', '/').Split('/').Last();
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Length > 255 || name.Any(char.IsControl))
        {
            throw new ArgumentException("File name must contain 1–255 characters and no control characters.");
        }
        return name;
    }

    public static string ContentType(string? value)
    {
        if (value is null || value.Length > 255 || !MediaTypeHeaderValue.TryParse(value, out var parsed)
            || string.IsNullOrEmpty(parsed.MediaType) || parsed.MediaType.Contains('*'))
        {
            return "application/octet-stream";
        }
        return parsed.MediaType.ToLowerInvariant();
    }
}
