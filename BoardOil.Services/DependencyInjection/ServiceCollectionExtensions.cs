using BoardOil.Abstractions;
using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.CardType;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.Image;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.OAuth;
using BoardOil.Abstractions.Tag;
using BoardOil.Abstractions.Users;
using BoardOil.Abstractions.Slick;
using BoardOil.Services.Auth;
using BoardOil.Services.Configuration;
using BoardOil.Services.Board;
using BoardOil.Services.Board.Import;
using BoardOil.Services.Card;
using BoardOil.Services.CardType;
using BoardOil.Services.Column;
using BoardOil.Services.Image;
using BoardOil.Services.ErrorLogs;
using BoardOil.Services.OAuth;
using BoardOil.Services.Tag;
using BoardOil.Services.Slick;
using BoardOil.Services.Style;
using BoardOil.Services.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilServices(this IServiceCollection services)
    {
        services.AddScoped<IColumnValidator, ColumnValidator>();
        services.AddScoped<ICardValidator, CardValidator>();
        services.AddScoped<CreateCardPlanner>();
        services.AddScoped<CreateCardService>();
        services.AddScoped<UpdateCardPlanner>();
        services.AddScoped<SlickCohesionPlacementResolver>();
        services.AddScoped<UpdateCardService>();
        services.AddScoped<CardSortKeyRenormaliser>();
        services.AddScoped<CardInsertionOrderPlanner>();
        services.AddScoped<CardMoveOrderPlanner>();
        services.AddScoped<MoveCardService>();
        services.AddScoped<BulkEditCardsService>();
        services.AddScoped<BulkDeleteCardsService>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IBoardBootstrapService, BoardBootstrapService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IBoardExportService, BoardExportService>();
        services.AddScoped<BoardPackageImportReader>();
        services.AddScoped<BoardPackageImportPlanner>();
        services.AddScoped<ImportedUserResolver>();
        services.AddScoped<BoardPackageImportWriter>();
        services.AddScoped<IBoardPackageImportService, BoardPackageImportService>();
        services.AddScoped<ISystemBoardService, SystemBoardService>();
        services.AddScoped<IBoardAuthorisationService, BoardAuthorisationService>();
        services.AddScoped<IBoardMemberService, BoardMemberService>();
        services.AddScoped<ISystemInfoMessageService, SystemInfoMessageService>();
        services.AddScoped<IErrorLogService, ErrorLogService>();
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ICardOptionsService, CardOptionsService>();
        services.AddScoped<ICardCommentService, CardCommentService>();
        services.AddScoped<ICardArchiveService, CardArchiveService>();
        services.AddScoped<ICardTypeService, CardTypeService>();
        services.AddScoped<IBoardStyleDefaultService, BoardStyleDefaultService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ISlickService, SlickService>();
        services.AddScoped<IClientAccountService, ClientAccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IImageStorageService, LocalImageStorageService>();
        services.AddScoped<IUserProfileImageService, UserProfileImageService>();
        services.AddScoped<IOAuthConnectionManagementService, OAuthConnectionManagementService>();
        services.AddScoped<IOAuthTokenAuditService, OAuthTokenAuditService>();
        return services;
    }
}
