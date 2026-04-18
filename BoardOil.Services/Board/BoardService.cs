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
    IBoardMemberRepository boardMemberRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IDbContextScopeFactory scopeFactory) : IBoardService
{
    private const int MaxBoardNameLength = 120;
    private const int MaxBoardDescriptionLength = 5_000;

    public async Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> GetBoardsAsync(int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var memberships = await boardMemberRepository.GetMembershipsForUserOrderedAsync(actorUserId);
        return memberships
            .Select(x => new BoardSummaryDto(
                x.Board.Id,
                x.Board.Name,
                x.Board.Description,
                x.Board.CreatedAtUtc,
                x.Board.UpdatedAtUtc,
                x.Role.ToString()))
            .ToList();
    }

    public async Task<ApiResult<BoardDto>> GetBoardAsync(int boardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var board = boardRepository.Get(boardId);
        if (board is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
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

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, actorUserId);
        var currentUserRole = membership?.Role.ToString();

        return new BoardDto(
            board.Id,
            board.Name,
            board.Description,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            currentUserRole,
            columnDtos);
    }

    public async Task<ApiResult<BoardDto>> CreateBoardAsync(CreateBoardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var name = request.Name.Trim();
        var description = NormaliseBoardDescription(request.Description);
        var validationError = ValidateBoardName(name);
        if (validationError is null)
        {
            validationError = ValidateBoardDescription(description);
        }

        if (validationError is not null)
        {
            return validationError;
        }

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = name,
            Description = description,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        board.Members.Add(new EntityBoardMember
        {
            UserId = actorUserId,
            Role = BoardMemberRole.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        boardRepository.Add(board);
        board.CardTypes.Add(CardTypeDefaults.CreateSystemForBoard(board, now));

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
            board.Description,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            BoardMemberRole.Owner.ToString(),
            columnDtos));
    }

    public async Task<ApiResult<BoardSummaryDto>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var board = boardRepository.Get(boardId);
        if (board is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var updatedName = request.Name.Trim();
        var updatedDescription = NormaliseBoardDescription(request.Description);
        var validationError = ValidateBoardName(updatedName);
        if (validationError is null)
        {
            validationError = ValidateBoardDescription(updatedDescription);
        }

        if (validationError is not null)
        {
            return validationError;
        }

        var hasNameChanged = !string.Equals(board.Name, updatedName, StringComparison.Ordinal);
        var hasDescriptionChanged = !string.Equals(board.Description, updatedDescription, StringComparison.Ordinal);
        if (hasNameChanged || hasDescriptionChanged)
        {
            board.Name = updatedName;
            board.Description = updatedDescription;
            board.UpdatedAtUtc = DateTime.UtcNow;
            await scope.SaveChangesAsync();
        }

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, actorUserId);
        return new BoardSummaryDto(
            board.Id,
            board.Name,
            board.Description,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            membership?.Role.ToString());
    }

    public async Task<ApiResult> DeleteBoardAsync(int boardId, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var board = boardRepository.Get(boardId);
        if (board is null)
        {
            return ApiResults.Ok();
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        boardRepository.Remove(board);
        await scope.SaveChangesAsync();
        return ApiResults.Ok();
    }

    private static ApiError? ValidateBoardName(string boardName)
    {
        if (string.IsNullOrWhiteSpace(boardName))
        {
            return ApiErrors.BadRequest(
                "Validation failed.",
                [new ValidationError("name", "Board name is required.")]);
        }

        if (boardName.Length > MaxBoardNameLength)
        {
            return ApiErrors.BadRequest(
                "Validation failed.",
                [new ValidationError("name", $"Board name must be {MaxBoardNameLength} characters or fewer.")]);
        }

        return null;
    }

    private static string NormaliseBoardDescription(string? boardDescription) =>
        boardDescription?.Trim() ?? string.Empty;

    private static ApiError? ValidateBoardDescription(string boardDescription)
    {
        if (boardDescription.Length > MaxBoardDescriptionLength)
        {
            return ApiErrors.BadRequest(
                "Validation failed.",
                [new ValidationError("description", $"Board description must be {MaxBoardDescriptionLength} characters or fewer.")]);
        }

        return null;
    }
}
