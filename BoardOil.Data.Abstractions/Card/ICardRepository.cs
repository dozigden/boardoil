using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Card;

public enum CardSearchField
{
    ExternalUrl
}

public enum CardSearchOperator
{
    Exact,
    Contains
}

public sealed record CardSearchCriterion(
    CardSearchField Field,
    CardSearchOperator Operator,
    string Value);

public interface ICardRepository : IRepositoryBase<EntityBoardCard>
{
    Task<EntityBoardCard?> GetWithTagsAndBoardAsync(int boardId, int boardCardId);
    Task<IReadOnlyList<EntityBoardCard>> GetWithTagsAndBoardByIdsAsync(int boardId, IReadOnlyList<int> boardCardIds);
    Task<IReadOnlyList<EntityBoardCard>> GetByBoardAndCardTypeAsync(int boardId, int cardTypeId);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds);
    Task<IReadOnlyList<EntityBoardCard>> SearchAsync(int boardId, IReadOnlyList<CardSearchCriterion> criteria);
}
