using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardCard>(ambientDbContextLocator), ICardRepository
{
    public async Task<EntityBoardCard?> GetWithTagsByIdAsync(int id)
    {
        var card = Get(id);
        if (card is null)
        {
            return null;
        }

        await DbContext.Entry(card)
            .Reference(x => x.CardType)
            .LoadAsync();
        if (card.AssignedUserId is not null)
        {
            await DbContext.Entry(card)
                .Reference(x => x.AssignedUser)
                .LoadAsync();
        }
        if (card.SlickId is not null)
        {
            await DbContext.Entry(card)
                .Reference(x => x.Slick)
                .LoadAsync();
        }

        await DbContext.Entry(card)
            .Collection(x => x.CardTags)
            .Query()
            .Include(x => x.Tag)
            .LoadAsync();
        return card;
    }

    public Task<EntityBoardCard?> GetWithTagsAndBoardAsync(int id) =>
        DbSet
            .Include(x => x.CardType)
            .Include(x => x.AssignedUser)
            .Include(x => x.Slick)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IReadOnlyList<EntityBoardCard>> GetWithTagsAndBoardByIdsAsync(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<EntityBoardCard>();
        }

        return await DbSet
            .Where(x => ids.Contains(x.Id))
            .AsSplitQuery()
            .Include(x => x.CardType)
            .Include(x => x.AssignedUser)
            .Include(x => x.Slick)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.Comments)
                .ThenInclude(x => x.AuthorUser)
            .Include(x => x.BoardColumn)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EntityBoardCard>> GetByBoardAndCardTypeAsync(int boardId, int cardTypeId) =>
        await DbSet
            .Where(x => x.CardTypeId == cardTypeId && x.BoardColumn.BoardId == boardId)
            .ToListAsync();

    public Task<bool> ColumnExistsAsync(int columnId) =>
        DbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId) =>
        await DbSet
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Include(x => x.CardType)
            .Include(x => x.AssignedUser)
            .Include(x => x.Slick)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds)
    {
        if (columnIds.Count == 0)
        {
            return Array.Empty<EntityBoardCard>();
        }

        return await DbSet
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .Include(x => x.CardType)
            .Include(x => x.AssignedUser)
            .Include(x => x.Slick)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EntityBoardCard>> SearchAsync(
        int boardId,
        IReadOnlyList<CardSearchCriterion> criteria)
    {
        var cards = await DbSet
            .Where(x => x.BoardColumn.BoardId == boardId)
            .OrderBy(x => x.BoardColumn.SortKey)
            .ThenBy(x => x.SortKey)
            .Include(x => x.CardType)
            .Include(x => x.AssignedUser)
            .Include(x => x.Slick)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .ToListAsync();

        return cards
            .Where(card => criteria.All(criterion => MatchesSearchCriterion(card, criterion)))
            .ToList();
    }

    private static bool MatchesSearchCriterion(
        EntityBoardCard card,
        CardSearchCriterion criterion)
    {
        if (criterion.Field != CardSearchField.ExternalUrl)
        {
            throw new ArgumentOutOfRangeException(nameof(criterion), criterion.Field, "Unsupported card search field.");
        }

        if (criterion.Operator == CardSearchOperator.Exact)
        {
            return string.Equals(card.ExternalUrl, criterion.Value, StringComparison.Ordinal);
        }

        if (criterion.Operator == CardSearchOperator.Contains)
        {
            return card.ExternalUrl?.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase) == true;
        }

        throw new ArgumentOutOfRangeException(nameof(criterion), criterion.Operator, "Unsupported card search operator.");
    }

}
