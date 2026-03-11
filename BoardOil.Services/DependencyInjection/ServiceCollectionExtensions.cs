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
        services.AddScoped<IBoardBootstrapService, BoardBootstrapService>();
        services.AddScoped<IColumnService, ColumnService>();

        return services;
    }

    public static async Task InitializeBoardOilAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IBoardBootstrapService>();
        await bootstrapper.EnsureDefaultBoardAsync(cancellationToken);
    }
}
