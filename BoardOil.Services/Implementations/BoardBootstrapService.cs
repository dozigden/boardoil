using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class BoardBootstrapService(BoardOilDbContext dbContext) : IBoardBootstrapService
{
    public async Task EnsureDefaultBoardAsync(CancellationToken cancellationToken = default)
    {
        var existingBoard = await dbContext.Boards.AnyAsync(cancellationToken);
        if (existingBoard)
        {
            return;
        }

        var now = DateTime.UtcNow;
        dbContext.Boards.Add(new Board
        {
            Name = "BoardOil",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
