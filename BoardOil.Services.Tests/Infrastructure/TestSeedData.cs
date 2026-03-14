using BoardOil.Ef.Entities;

namespace BoardOil.Services.Tests.Infrastructure;

public static class TestSeedData
{
    public static readonly DateTime FixedNow = new(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);

    public static readonly string[] OrderedSortKeys =
    [
        "10000000000000000000",
        "30000000000000000000",
        "50000000000000000000",
        "70000000000000000000",
        "90000000000000000000"
    ];

    public static async Task<Board> SeedDefaultBoardAsync(BoardOil.Ef.BoardOilDbContext db, string name = "BoardOil")
    {
        var board = new Board
        {
            Name = name,
            CreatedAtUtc = FixedNow,
            UpdatedAtUtc = FixedNow
        };

        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    public static async Task<IReadOnlyList<BoardColumn>> SeedColumnsAsync(
        BoardOil.Ef.BoardOilDbContext db,
        int boardId,
        params string[] titles)
    {
        var columns = titles
            .Select((title, index) => new BoardColumn
            {
                BoardId = boardId,
                Title = title,
                Position = index,
                CreatedAtUtc = FixedNow,
                UpdatedAtUtc = FixedNow
            })
            .ToList();

        db.Columns.AddRange(columns);
        await db.SaveChangesAsync();
        return columns;
    }

    public static async Task<IReadOnlyList<BoardCard>> SeedCardsAsync(
        BoardOil.Ef.BoardOilDbContext db,
        int columnId,
        params (string Title, string Description, string SortKey)[] cards)
    {
        var entities = cards
            .Select(card => new BoardCard
            {
                BoardColumnId = columnId,
                Title = card.Title,
                Description = card.Description,
                SortKey = card.SortKey,
                CreatedAtUtc = FixedNow,
                UpdatedAtUtc = FixedNow
            })
            .ToList();

        db.Cards.AddRange(entities);
        await db.SaveChangesAsync();
        return entities;
    }
}
