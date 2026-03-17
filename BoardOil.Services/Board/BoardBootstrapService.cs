using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;

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
        boardRepository.Add(new BoardCreateRecord(
            Name: "BoardOil",
            CreatedAtUtc: now,
            UpdatedAtUtc: now));

        await boardRepository.SaveChangesAsync();
    }
}
