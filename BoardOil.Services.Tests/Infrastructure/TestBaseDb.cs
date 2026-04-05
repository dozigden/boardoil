using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.CardType;
using BoardOil.Services.Column;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Tag;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BoardOil.Persistence.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests.Infrastructure;

public abstract class TestBaseDb : IAsyncLifetime
{
    private static readonly DateTime FixedNow = new(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);
    private SqliteTestHarness? _harness;
    private BoardOilDbContext? _dbContextForArrange;
    private BoardOilDbContext? _dbContextForAssert;
    private readonly List<BoardOilDbContext> _actDbContexts = [];
    private ServiceProvider? _serviceProvider;
    private readonly List<IServiceScope> _serviceScopes = [];
    protected int ActorUserId { get; private set; }

    protected BoardOilDbContext DbContextForArrange =>
        _dbContextForArrange ??= Harness.CreateDbContext();

    protected BoardOilDbContext DbContextForAssert =>
        _dbContextForAssert ??= Harness.CreateDbContext();

    protected BoardOilDbContext CreateDbContextForAct()
    {
        var dbContext = Harness.CreateDbContext();
        _actDbContexts.Add(dbContext);
        return dbContext;
    }

    protected FluentBoardBuilder CreateBoard(string name = "BoardOil") =>
        new(DbContextForArrange, name, FixedNow, ActorUserId);

    protected T ResolveService<T>() where T : notnull
    {
        var scope = ServiceProvider.CreateScope();
        _serviceScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    private SqliteTestHarness Harness =>
        _harness ?? throw new InvalidOperationException("Test harness has not been initialized.");

    private ServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException("Service provider has not been initialized.");

    public async Task InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();

        await using (var setupDb = Harness.CreateDbContext())
        {
            var actorUser = new EntityUser
            {
                UserName = "actor",
                PasswordHash = "test-hash",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAtUtc = FixedNow,
                UpdatedAtUtc = FixedNow
            };
            setupDb.Users.Add(actorUser);
            await setupDb.SaveChangesAsync();
            ActorUserId = actorUser.Id;
        }

        var services = new ServiceCollection();
        services.AddBoardOilServices("DataSource=:memory:");
        services.AddScoped<BoardService>();
        services.AddScoped<ColumnService>();
        services.AddScoped<CardService>();
        services.AddScoped<CardTypeService>();
        services.AddScoped<TagService>();
        services.AddSingleton<IBoardEvents, TestBoardEvents>();
        services.AddSingleton<IDbContextFactory>(_ => new TestDbContextFactory(Harness.Options));
        ConfigureTestServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        foreach (var serviceScope in _serviceScopes)
        {
            serviceScope.Dispose();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        foreach (var actDbContext in _actDbContexts)
        {
            await actDbContext.DisposeAsync();
        }

        if (_dbContextForAssert is not null)
        {
            await _dbContextForAssert.DisposeAsync();
        }

        if (_dbContextForArrange is not null)
        {
            await _dbContextForArrange.DisposeAsync();
        }

        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    protected static async Task<List<string>> GetOrderedTitlesAsync(BoardOilDbContext db, int columnId) =>
        await db.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        _ = services;
    }
}
