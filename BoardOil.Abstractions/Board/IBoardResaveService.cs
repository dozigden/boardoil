namespace BoardOil.Abstractions.Board;

public interface IBoardResaveService
{
    Task<BoardResaveResult> ResaveAsync(int boardId, CancellationToken cancellationToken = default);
}

public sealed record BoardResaveResult(int BoardId, bool Skipped, int CardsExamined, int CardsChanged);
