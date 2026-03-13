using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;

namespace BoardOil.Services.Implementations;

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
        boardRepository.Add(new Board
        {
            Name = "BoardOil",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await boardRepository.SaveChangesAsync();
    }
}
