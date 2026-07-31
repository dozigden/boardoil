namespace BoardOil.Data.Abstractions.Card;

public interface IBoardCardIdAllocator
{
    Task<int> AllocateNextAsync(int boardId, CancellationToken cancellationToken = default);
}
