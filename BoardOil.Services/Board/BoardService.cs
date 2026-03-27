using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Services.Card;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Board;

public sealed class BoardService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    IDbContextScopeFactory scopeFactory) : IBoardService
{
    public async Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> GetBoardsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var boards = await boardRepository.GetBoardsOrderedAsync();
        var dto = boards
            .Select(x => new BoardSummaryDto(x.Id, x.Name, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToList();
        return dto;
    }

    public async Task<ApiResult<BoardDto>> GetBoardAsync(int boardId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var board = boardRepository.Get(boardId);
        if (board is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);

        var columnIds = columns.Select(x => x.Id).ToList();
        var cards = await cardRepository.GetCardsForColumnsOrderedAsync(columnIds);

        var cardsByColumnId = cards
            .GroupBy(x => x.BoardColumnId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<CardDto>)x
                    .Select(card => card.ToCardDto())
                    .ToList());

        var columnDtos = columns
            .Select(x => new BoardColumnDto(
                x.Id,
                x.Title,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                cardsByColumnId.GetValueOrDefault(x.Id, Array.Empty<CardDto>())))
            .ToList();

        return new BoardDto(
            board.Id,
            board.Name,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            columnDtos);
    }

    public async Task<ApiResult<BoardDto>> CreateBoardAsync(CreateBoardRequest request)
    {
        using var scope = scopeFactory.Create();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiErrors.BadRequest(
                "Validation failed.",
                [new ValidationError("name", "Board name is required.")]);
        }

        if (name.Length > 120)
        {
            return ApiErrors.BadRequest(
                "Validation failed.",
                [new ValidationError("name", "Board name must be 120 characters or fewer.")]);
        }

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        boardRepository.Add(board);

        var seedTitles = new[] { "Todo", "In Progress", "Done" };
        string? previousSortKey = null;
        var createdColumns = new List<EntityBoardColumn>(seedTitles.Length);
        foreach (var title in seedTitles)
        {
            var sortKey = SortKeyGenerator.Between(previousSortKey, null);
            var column = new EntityBoardColumn
            {
                Board = board,
                Title = title,
                SortKey = sortKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            columnRepository.Add(column);
            createdColumns.Add(column);
            previousSortKey = sortKey;
        }

        await scope.SaveChangesAsync();

        var columnDtos = createdColumns
            .OrderBy(x => x.SortKey)
            .Select(x => new BoardColumnDto(
                x.Id,
                x.Title,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                Array.Empty<CardDto>()))
            .ToList();

        return ApiResults.Created(new BoardDto(
            board.Id,
            board.Name,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            columnDtos));
    }
}
