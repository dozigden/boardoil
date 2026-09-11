using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.Board;

public sealed class BoardPackageStorageService(
    AttachmentStorageOptions options,
    ITemporaryBoardPackageRepository packages,
    IDbContextScopeFactory scopes,
    ILogger<BoardPackageStorageService>? logger = null)
{
    public async Task<FileStream> CreateAsync(CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid().ToString("N");
        using (scopes.SuppressAmbientContext())
        using (var scope = scopes.Create())
        {
            packages.Add(new EntityTemporaryBoardPackage { StorageKey = key });
            await scope.SaveChangesAsync(cancellationToken);
        }
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new TemporaryPackageStream(path, () => DeleteAsync(key, CancellationToken.None));
    }

    // Only call before serving requests; active package streams must not be removed.
    public async Task<int> CleanupAtStartupAsync(CancellationToken cancellationToken = default)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        List<string> keys;
        using (var scope = scopes.CreateReadOnly())
        {
            keys = await packages.Query().Select(x => x.StorageKey).ToListAsync(cancellationToken);
        }
        var deleted = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            deleted += await DeleteAsync(key, cancellationToken);
        }
        return deleted;
    }

    private async Task<int> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        using var scope = scopes.Create();
        try
        {
            var record = await packages.Query().SingleOrDefaultAsync(x => x.StorageKey == key, cancellationToken).ConfigureAwait(false);
            if (record is null) { return 0; }
            try { File.Delete(ResolvePath(key)); }
            catch (DirectoryNotFoundException) { /* No file remains to remove. */ }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                record.LastError = exception.Message;
                await scope.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger?.LogError(exception, "Could not delete temporary package {StorageKey}; retained for next startup.", key);
                return 0;
            }
            packages.Remove(record);
            await scope.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (Exception exception) when (exception is DbUpdateException or System.Data.Common.DbException)
        {
            // A completed download must not fail because its recovery record could not be removed.
            logger?.LogError(exception, "Could not remove temporary package record {StorageKey}; retained for next startup.", key);
            return 0;
        }
    }

    private string ResolvePath(string key)
    {
        if (key.Length != 32 || !key.All(char.IsAsciiHexDigit)) { throw new ArgumentException("Invalid package storage key."); }
        var directory = string.IsNullOrWhiteSpace(options.RootPath) ? Path.GetTempPath() : Path.Combine(options.RootPath, "packages");
        return Path.Combine(directory, "boardoil-package-" + key + ".tmp");
    }

    // The stream owns its file and tracking row, including when the HTTP response disposes it.
    private sealed class TemporaryPackageStream(string path, Func<Task> cleanup)
        : FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous)
    {
        private int _cleanupStarted;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { CleanupOnceAsync().GetAwaiter().GetResult(); }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await CleanupOnceAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private Task CleanupOnceAsync() => Interlocked.Exchange(ref _cleanupStarted, 1) == 0
            ? cleanup()
            : Task.CompletedTask;
    }
}
