using BoardOil.Abstractions.Attachment;
using Microsoft.Data.Sqlite;

namespace BoardOil.Api.Configuration;

public static class BoardOilAttachmentStorageOptions
{
    public static AttachmentStorageOptions Resolve(IConfiguration configuration, string connectionString, string imageRoot, string webRoot)
    {
        var database = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var defaultRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!, "attachments");
        var root = ResolvePhysicalPath(configuration["BoardOil:AttachmentRootPath"] ?? defaultRoot);
        foreach (var publicRoot in new[] { imageRoot, webRoot })
        {
            var relative = Path.GetRelativePath(ResolvePhysicalPath(publicRoot), root);
            if (relative == "." || (!relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && relative != ".." && !Path.IsPathRooted(relative)))
            {
                throw new InvalidOperationException("Attachment storage must be outside publicly served directories.");
            }
        }
        var options = new AttachmentStorageOptions
        {
            RootPath = root,
            MaxUploadByteLength = configuration.GetValue<long?>("BoardOil:AttachmentMaxByteLength") ?? 10 * 1024 * 1024,
            MaxImagePixelCount = configuration.GetValue<long?>("BoardOil:AttachmentImageMaxPixelCount") ?? 20_000_000,
            MaxImageEdgeLength = configuration.GetValue<int?>("BoardOil:AttachmentImageMaxEdgeLength") ?? 10_000
        };
        if (options.MaxUploadByteLength <= 0
            || options.MaxUploadByteLength > long.MaxValue - 65536)
        {
            throw new InvalidOperationException("Attachment size limit must be positive and allow multipart overhead.");
        }
        if (options.MaxImagePixelCount <= 0 || options.MaxImageEdgeLength <= 0)
        {
            throw new InvalidOperationException("Attachment image limits must be positive.");
        }
        Directory.CreateDirectory(root);
        return options;
    }

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var resolved = Path.GetPathRoot(fullPath)!;
        foreach (var segment in fullPath[resolved.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            if (!Directory.Exists(resolved)) { continue; }
            var target = new DirectoryInfo(resolved).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null) { resolved = target.FullName; }
        }
        return resolved;
    }
}
