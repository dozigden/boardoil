using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Ef;
using BoardOil.Services.Attachment;
using BoardOil.Services.Board;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AttachmentStartupIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationStartup_ShouldRecoverPendingFilesBeforeServingRequests(bool pendingDeletion)
    {
        var databasePath = ApiFactoryIntegrationTestBase.BuildDbPath(nameof(AttachmentStartupIntegrationTests));
        string key;
        string packagePath;
        await using (var first = new BoardOilApiFactory(databasePath))
        {
            using var client = first.CreateClient();
            using var scope = first.Services.CreateScope();
            var pending = await scope.ServiceProvider.GetRequiredService<CardAttachmentService>()
                .PrepareAsync(new MemoryStream([1, 2, 3]), "interrupted.bin", null, null);
            key = pending.StorageKey;
            if (pendingDeletion)
            {
                await scope.ServiceProvider.GetRequiredService<CardAttachmentService>().AbandonAsync(pending.Id);
            }
            var package = await scope.ServiceProvider.GetRequiredService<BoardPackageStorageService>().CreateAsync();
            packagePath = package.Name;
            await using var setup = scope.ServiceProvider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var record = await setup.TemporaryBoardPackages.AsNoTracking().SingleAsync();
            await package.DisposeAsync();
            // Simulate interruption after record creation but before normal stream disposal.
            record.Id = 0;
            setup.TemporaryBoardPackages.Add(record);
            await setup.SaveChangesAsync();
            await File.WriteAllBytesAsync(packagePath, [4, 5, 6]);
        }

        await using var restarted = new BoardOilApiFactory(databasePath);
        using var restartedClient = restarted.CreateClient();
        (await restartedClient.GetAsync("/api/health")).EnsureSuccessStatusCode();

        using var assertScope = restarted.Services.CreateScope();
        var factory = assertScope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var database = factory.CreateDbContext<BoardOilDbContext>();
        Assert.Empty(await database.CardAttachments.ToListAsync());
        Assert.Empty(await database.TemporaryBoardPackages.ToListAsync());
        Assert.False(File.Exists(packagePath));
        Assert.Throws<FileNotFoundException>(() => assertScope.ServiceProvider.GetRequiredService<IAttachmentStorageService>().OpenRead(key));
    }
}
