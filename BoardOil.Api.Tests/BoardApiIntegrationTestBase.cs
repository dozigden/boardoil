using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Ordering;
using BoardOil.Ef;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tag;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Api.Tests;

public abstract class BoardApiIntegrationTestBase : TestBaseIntegration
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards.AnyAsync(x => x.Id == 1);
            if (!boardExists)
            {
                throw new InvalidOperationException("Board with id 1 was not found.");
            }

            var now = DateTime.UtcNow;
            dbContext.Tags.Add(new EntityTag
            {
                BoardId = 1,
                Name = name,
                NormalisedName = normalisedName,
                StyleName = styleName,
                StylePropertiesJson = stylePropertiesJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        });
    }

    protected async Task<int> SeedBoardTagAsync(string name, int boardId = 1, string? emoji = null)
    {
        return await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards.AnyAsync(x => x.Id == boardId);
            if (!boardExists)
            {
                throw new InvalidOperationException($"Board with id {boardId} was not found.");
            }

            var canonicalName = name.Trim();
            var now = DateTime.UtcNow;
            var tag = new EntityTag
            {
                BoardId = boardId,
                Name = canonicalName,
                NormalisedName = canonicalName.ToUpperInvariant(),
                StyleName = TagStyleSchemaValidator.SolidStyleName,
                StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
                Emoji = emoji,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Tags.Add(tag);
            return tag.Id;
        });
    }

    protected async Task AssignCardTypeToCardAsync(int cardId, int cardTypeId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE "Cards"
            SET "CardTypeId" = $cardTypeId
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$cardTypeId", cardTypeId);
        await command.ExecuteNonQueryAsync();
    }

    protected async Task<int> GetCardTypeIdForCardAsync(int cardId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "CardTypeId"
            FROM "Cards"
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    protected async Task<T> UseDbContextAsync<T>(Func<BoardOilDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        return await action(dbContext);
    }

    protected async Task UseDbContextAsync(Func<BoardOilDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        await action(dbContext);
    }

    protected async Task<T> ArrangeAsync<T>(Func<BoardOilDbContext, Task<T>> arrange)
    {
        return await UseDbContextAsync(async dbContext =>
        {
            var result = await arrange(dbContext);
            await dbContext.SaveChangesAsync();
            return result;
        });
    }

    protected async Task ArrangeAsync(Func<BoardOilDbContext, Task> arrange)
    {
        await UseDbContextAsync(async dbContext =>
        {
            await arrange(dbContext);
            await dbContext.SaveChangesAsync();
        });
    }

    protected async Task<int> SeedBoardColumnAsync(string title, int boardId = 1)
    {
        var column = await ArrangeAsync(async dbContext =>
        {
            var boardExists = await dbContext.Boards
                .AnyAsync(x => x.Id == boardId);
            if (!boardExists)
            {
                throw new InvalidOperationException($"Board with id {boardId} was not found.");
            }

            var previousSortKey = await dbContext.Columns
                .Where(x => x.BoardId == boardId)
                .OrderByDescending(x => x.SortKey)
                .Select(x => x.SortKey)
                .FirstOrDefaultAsync();

            var now = DateTime.UtcNow;
            var column = new EntityBoardColumn
            {
                BoardId = boardId,
                Title = title,
                SortKey = SortKeyGenerator.Between(previousSortKey, null),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Columns.Add(column);
            return column;
        });

        return column.Id;
    }

    protected async Task<int> SeedBoardCardAsync(int columnId, string title, string description)
    {
        return await UseDbContextAsync(async dbContext =>
        {
            var column = await dbContext.Columns.FindAsync(columnId);
            if (column is null)
            {
                throw new InvalidOperationException($"Column with id {columnId} was not found.");
            }

            var now = DateTime.UtcNow;
            var board = await dbContext.Boards.FindAsync(column.BoardId);
            if (board is null)
            {
                throw new InvalidOperationException($"Board with id {column.BoardId} was not found.");
            }

            var cardType = await dbContext.CardTypes
                .Where(x => x.BoardId == column.BoardId && x.IsSystem)
                .FirstOrDefaultAsync();

            if (cardType is null)
            {
                cardType = CardTypeDefaults.CreateSystemForBoard(board, now);
                dbContext.CardTypes.Add(cardType);
            }

            var previousSortKey = await dbContext.Cards
                .Where(x => x.BoardColumnId == columnId)
                .OrderByDescending(x => x.SortKey)
                .Select(x => x.SortKey)
                .FirstOrDefaultAsync();

            var card = new EntityBoardCard
            {
                BoardColumnId = columnId,
                CardType = cardType,
                Title = title,
                Description = description,
                SortKey = SortKeyGenerator.Between(previousSortKey, null),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            return card.Id;
        });
    }

    protected sealed record ApiEnvelope<T>(
        bool Success,
        T? Data,
        int StatusCode,
        string? Message,
        Dictionary<string, string[]>? ValidationErrors = null);
}
