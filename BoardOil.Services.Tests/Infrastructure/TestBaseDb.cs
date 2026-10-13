using BoardOil.Contracts.CustomField;
using BoardOil.Services.CustomField;
using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.CardType;
using BoardOil.Services.Column;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Tag;
using BoardOil.Services.Slick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BoardOil.Data.Abstractions.Entities;
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

    protected static void SetFields(EntityCardType type, IReadOnlyList<CustomFieldDefinition> definitions)
    {
        var fields = CustomFieldCatalogue.Read(type.Board.CustomFields).ToList();
        fields = CustomFieldCatalogue.RemoveType(fields, type.Id).ToList();
        foreach (var definition in definitions)
        {
            var index = fields.FindIndex(field => field.Definition.Code == definition.Code);
            if (index >= 0)
            {
                var selected = (SelectedCardTypes)fields[index].AppliesTo;
                fields[index] = new(definition, new SelectedCardTypes([..selected.CardTypeIds, type.Id]));
            }
            else { fields.Add(new(definition, new SelectedCardTypes([type.Id]))); }
        }
        // Seed only the definitions used by this fixture. Unused-definition behaviour is tested through services.
        fields.RemoveAll(field => field.AppliesTo is SelectedCardTypes selected && selected.CardTypeIds.Count == 0);
        CustomFieldCatalogue.Apply(type.Board, fields);
    }

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

    public async ValueTask InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();

        await using (var setupDb = Harness.CreateDbContext())
        {
            var actorUser = new EntityUser
            {
                UserName = "actor",
                Email = "actor@localhost",
                NormalisedEmail = "actor@localhost",
                PasswordHash = "test-hash",
                Role = UserRole.Admin,
                IsActive = true,
            };
            setupDb.Users.Add(actorUser);
            await setupDb.SaveChangesAsync();
            ActorUserId = actorUser.Id;
        }

        var services = new ServiceCollection();
        services.AddBoardOilServices();
        services.AddBoardOilEfInfrastructure("DataSource=:memory:");
        services.AddScoped<BoardService>();
        services.AddScoped<ColumnService>();
        services.AddScoped<CardService>();
        services.AddScoped<CardTypeService>();
        services.AddScoped<TagService>();
        services.AddScoped<SlickService>();
        services.AddSingleton<IBoardEvents, TestBoardEvents>();
        services.AddSingleton<IDbContextFactory>(_ => new TestDbContextFactory(Harness.Options));
        ConfigureTestServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
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
