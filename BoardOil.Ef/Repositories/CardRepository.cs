using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardRepository(BoardOilDbContext dbContext) : ICardRepository
{
    public Task<CardRecord?> GetByIdAsync(int id) =>
        dbContext.Cards
            .Where(x => x.Id == id)
            .Select(x => new CardRecord(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstOrDefaultAsync();

    public Task<bool> ColumnExistsAsync(int columnId) =>
        dbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<CardRecord>> GetCardsInColumnOrderedAsync(int columnId) =>
        await dbContext.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => new CardRecord(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

    public async Task<IReadOnlyList<CardRecord>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds)
    {
        if (columnIds.Count == 0)
        {
            return Array.Empty<CardRecord>();
        }

        return await dbContext.Cards
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .Select(x => new CardRecord(
                x.Id,
                x.BoardColumnId,
                x.Title,
                x.Description,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();
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

        await dbContext.SaveChangesAsync();

        return new CardRecord(
            entity.Id,
            entity.BoardColumnId,
            entity.Title,
            entity.Description,
            entity.SortKey,
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
}
