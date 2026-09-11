using BoardOil.Services.Attachment;
using BoardOil.Services.Board;

namespace BoardOil.Api.Configuration;

public static class AttachmentStartupExtensions
{
    public static async Task CleanupAttachmentsAtStartupAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CardAttachmentService>().CleanupAtStartupAsync();
        await scope.ServiceProvider.GetRequiredService<BoardPackageStorageService>().CleanupAtStartupAsync();
    }
}
