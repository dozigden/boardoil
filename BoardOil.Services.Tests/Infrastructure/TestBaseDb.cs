using System.Text.Json;
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
        foreach (var field in type.Board.CustomFields)
        {
            foreach (var assignment in field.CardTypeAssignments.Where(assignment => assignment.CardTypeId == type.Id).ToArray())
            {
                field.CardTypeAssignments.Remove(assignment);
            }
        }
        foreach (var definition in definitions)
        {
            var field = type.Board.CustomFields.SingleOrDefault(field => field.Code == definition.Code);
            if (field is null)
            {
                AddCustomField(type.Board, new(definition, new SelectedCardTypes([type.Id])));
            }
            else
            {
                var typeIds = field.CardTypeAssignments.Select(assignment => assignment.CardTypeId).Append(type.Id).ToArray();
                field.ApplyCustomFieldDraft(new(definition, new SelectedCardTypes(typeIds)));
            }
        }
        // Seed only the definitions used by this fixture. Unused-definition behaviour is tested through services.
        foreach (var unused in type.Board.CustomFields.Where(field => !field.AppliesToAllCardTypes && field.CardTypeAssignments.Count == 0).ToArray())
        {
            type.Board.CustomFields.Remove(unused);
        }
    }

    protected static EntityCustomField AddCustomField(EntityBoard board, CustomFieldDraft draft)
    {
        var field = new EntityCustomField
        {
            Board = board,
            BoardId = board.Id,
            SortOrder = board.CustomFields.Select(existing => existing.SortOrder).DefaultIfEmpty(-1).Max() + 1
        };
        field.ApplyCustomFieldDraft(draft);
        board.CustomFields.Add(field);
        return field;
    }

    protected static CustomFieldDefinitionInput ToCustomFieldInput(CustomFieldDefinition definition)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize<CustomFieldDefinition>(definition, options);
        return JsonSerializer.Deserialize<CustomFieldDefinitionInput>(json, options)!;
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
