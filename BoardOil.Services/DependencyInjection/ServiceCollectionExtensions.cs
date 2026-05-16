using BoardOil.Abstractions;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.CardType;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.Image;
using BoardOil.Abstractions.Tag;
using BoardOil.Abstractions.Users;
using BoardOil.Abstractions.Slick;
using BoardOil.Services.Auth;
using BoardOil.Services.Board;
using BoardOil.Services.Board.Import;
using BoardOil.Services.Card;
using BoardOil.Services.CardType;
using BoardOil.Services.Column;
using BoardOil.Services.Image;
using BoardOil.Services.Tag;
using BoardOil.Services.Slick;
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
        services.AddScoped<UpdateCardService>();
        services.AddScoped<MoveCardPlanner>();
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
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ICardCommentService, CardCommentService>();
        services.AddScoped<ICardArchiveService, CardArchiveService>();
        services.AddScoped<ICardTypeService, CardTypeService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ISlickService, SlickService>();
        services.AddScoped<IClientAccountService, ClientAccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IImageStorageService, LocalImageStorageService>();
        services.AddScoped<IUserProfileImageService, UserProfileImageService>();
        return services;
    }
}
