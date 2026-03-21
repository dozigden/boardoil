using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardRepository(BoardOilDbContext dbContext) : ICardRepository
{
    public async Task<CardRecord?> GetByIdAsync(int id)
    {
        var card = await dbContext.Cards
            .Where(x => x.Id == id)
            .Select(x => new CardRow(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstOrDefaultAsync();
        if (card is null)
        {
            return null;
        }

        var tags = await GetCardTagNamesMapAsync([card.Id]);
        return card.ToCardRecord(tags.GetValueOrDefault(card.Id, Array.Empty<string>()));
    }

    public Task<bool> ColumnExistsAsync(int columnId) =>
        dbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<CardRecord>> GetCardsInColumnOrderedAsync(int columnId)
    {
        var rows = await dbContext.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => new CardRow(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

        return await MapRowsToRecordsAsync(rows);
    }

    public async Task<IReadOnlyList<CardRecord>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds)
    {
        if (columnIds.Count == 0)
        {
            return Array.Empty<CardRecord>();
        }

        var rows = await dbContext.Cards
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .Select(x => new CardRow(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

        return await MapRowsToRecordsAsync(rows);
    }

    public async Task<IReadOnlyList<int>> GetCardIdsInColumnOrderedAsync(int columnId) =>
        await dbContext.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Id)
            .ToListAsync();

    public async Task<CardRecord> CreateAsync(CreateCardRecord card)
    {
        var entity = dbContext.Cards.Add(new BoardOil.Ef.Entities.BoardCard
        {
            BoardColumnId = card.BoardColumnId,
            Title = card.Title,
            Description = card.Description,
            SortKey = card.SortKey,
            CreatedAtUtc = card.CreatedAtUtc,
            UpdatedAtUtc = card.UpdatedAtUtc
        }).Entity;

        AddCardTags(entity, card.TagNames);
        await dbContext.SaveChangesAsync();

        return new CardRecord(
            entity.Id,
            entity.BoardColumnId,
            entity.Title,
            entity.Description,
            entity.SortKey,
            NormalizeTags(card.TagNames),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    public async Task UpdateAsync(UpdateCardRecord card)
    {
        var entity = await dbContext.Cards.FirstOrDefaultAsync(x => x.Id == card.Id);
        if (entity is null)
        {
            return;
        }

        entity.BoardColumnId = card.BoardColumnId;
        entity.Title = card.Title;
        entity.Description = card.Description;
        entity.SortKey = card.SortKey;
        entity.UpdatedAtUtc = card.UpdatedAtUtc;

        if (card.TagNames is not null)
        {
            var existingTags = await dbContext.CardTags
                .Where(x => x.CardId == card.Id)
                .ToListAsync();
            if (existingTags.Count > 0)
            {
                dbContext.CardTags.RemoveRange(existingTags);
            }

            var normalizedTagNames = NormalizeTags(card.TagNames);
            if (normalizedTagNames.Count > 0)
            {
                dbContext.CardTags.AddRange(normalizedTagNames.Select(tagName => new CardTag
                {
                    CardId = card.Id,
                    TagName = tagName
                }));
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await dbContext.Cards.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return;
        }

        dbContext.Cards.Remove(entity);
        await dbContext.SaveChangesAsync();
    }

    private void AddCardTags(BoardCard card, IReadOnlyList<string> tagNames)
    {
        foreach (var tagName in NormalizeTags(tagNames))
        {
            card.CardTags.Add(new CardTag { TagName = tagName });
        }
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tagNames) =>
        tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetCardTagNamesMapAsync(IReadOnlyList<int> cardIds)
    {
        if (cardIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
        }

        var flatRows = await dbContext.CardTags
            .Where(x => cardIds.Contains(x.CardId))
            .OrderBy(x => x.TagName)
            .Select(x => new
            {
                x.CardId,
                x.TagName
            })
            .ToListAsync();

        return flatRows
            .GroupBy(x => x.CardId)
            .ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<string>)x.Select(y => y.TagName).ToList());
    }

    private async Task<IReadOnlyList<CardRecord>> MapRowsToRecordsAsync(IReadOnlyList<CardRow> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<CardRecord>();
        }

        var ids = rows.Select(x => x.Id).ToList();
        var tagMap = await GetCardTagNamesMapAsync(ids);
        return rows
            .Select(x => x.ToCardRecord(tagMap.GetValueOrDefault(x.Id, Array.Empty<string>())))
            .ToList();
    }

    private sealed record CardRow(
        int Id,
        int BoardColumnId,
        string Title,
        string Description,
        string SortKey,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc)
    {
        public CardRecord ToCardRecord(IReadOnlyList<string> tagNames) =>
            new(
                Id,
                BoardColumnId,
                Title,
                Description,
                SortKey,
                tagNames,
                CreatedAtUtc,
                UpdatedAtUtc);
    }
}
