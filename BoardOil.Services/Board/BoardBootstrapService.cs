using BoardOil.Ef.Entities;
using BoardEntity = BoardOil.Ef.Entities.Board;

namespace BoardOil.Services.Board;

public sealed class BoardBootstrapService(IBoardRepository boardRepository) : IBoardBootstrapService
{
    public async Task EnsureDefaultBoardAsync()
    {
        var existingBoard = await boardRepository.AnyBoardAsync();
        if (existingBoard)
        {
            return;
        }

        var now = DateTime.UtcNow;
        boardRepository.Add(new BoardEntity
        {
            Name = "BoardOil",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await boardRepository.SaveChangesAsync();
    }
}
