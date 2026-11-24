using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Auth;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.ErrorLogs;
using BoardOil.Data.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.OAuth;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Ef.Context;
using BoardOil.Ef.Diagnostics;
using BoardOil.Ef.Card;
using BoardOil.Ef.Repositories;
using BoardOil.Ef.Scope;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Ef.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilEfInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddLogging();
        services.AddSingleton(provider => new SqliteLockDiagnostics(
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqliteLockDiagnostics>>(), connectionString));
        services.AddSingleton<IDatabaseFailureDiagnostics>(provider => provider.GetRequiredService<SqliteLockDiagnostics>());
        services.AddTransient<IDatabaseInfoProvider, SqliteDatabaseInfoProvider>();
        services.AddDbContext<BoardOilDbContext>(options =>
            options
                .UseSqlite(connectionString)
                .UseOpenIddict());
        services.AddSingleton<IDbContextFactory>(provider => new BoardOilDbContextFactory(
            connectionString, provider.GetRequiredService<SqliteLockDiagnostics>()));
        services.AddTransient<IDbContextScopeFactory, DbContextScopeFactory>();
        services.AddTransient<IAmbientDbContextLocator, AmbientDbContextLocator>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IBoardMemberRepository, BoardMemberRepository>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPersonalAccessTokenRepository, PersonalAccessTokenRepository>();
        services.AddScoped<IOAuthConnectionRepository, OAuthConnectionRepository>();
        services.AddScoped<IOAuthTokenAuditRepository, OAuthTokenAuditRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<ISystemInfoMessageRepository, SystemInfoMessageRepository>();
        services.AddScoped<IErrorLogRepository, ErrorLogRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobLogRepository, JobLogRepository>();
        services.AddScoped<IScheduledJobSchedulerStateRepository, ScheduledJobSchedulerStateRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IBoardCardIdAllocator, BoardCardIdAllocator>();
        services.AddScoped<ICardCommentRepository, CardCommentRepository>();
        services.AddScoped<IArchivedCardRepository, ArchivedCardRepository>();
        services.AddScoped<ICardTypeRepository, CardTypeRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ISlickRepository, SlickRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentDownloadTicketRepository, AttachmentDownloadTicketRepository>();
        services.AddScoped<IAttachmentUploadTicketRepository, AttachmentUploadTicketRepository>();
        services.AddScoped<IAttachmentTransferAuditRepository, AttachmentTransferAuditRepository>();
        services.AddScoped<ITemporaryBoardPackageRepository, TemporaryBoardPackageRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }

    public static async Task InitializeBoardOilEfInfrastructureAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IBoardBootstrapService>();
        await bootstrapper.EnsureDefaultBoardAsync();
        await scope.ServiceProvider.GetRequiredService<SqliteLockDiagnostics>().ReportJournalModeAsync(dbContext);
    }
}
