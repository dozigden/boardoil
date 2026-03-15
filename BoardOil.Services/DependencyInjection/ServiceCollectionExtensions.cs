using BoardOil.Ef;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BoardOilDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IColumnValidator, ColumnValidator>();
        services.AddScoped<ICardValidator, CardValidator>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IBoardBootstrapService, BoardBootstrapService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardService, CardService>();

        return services;
    }

    public static async Task InitializeBoardOilAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
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
    }
}
