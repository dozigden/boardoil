using System.Data;
using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Services.Jobs;

namespace BoardOil.Services.Board;

public sealed class SystemBoardService(
    IBoardRepository boardRepository,
    IBoardMemberRepository boardMemberRepository,
    IUserRepository userRepository,
    IJobService jobs,
    IDbContextScopeFactory scopeFactory) : ISystemBoardService
{
    public async Task<ApiResult<IReadOnlyList<SystemBoardSummaryDto>>> GetBoardsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var boards = await boardRepository.GetBoardsOrderedAsync();
        return boards.Select(ToSummary).ToList();
    }

    public async Task<ApiResult<IReadOnlyList<DeletedBoardSummaryDto>>> GetDeletedBoardsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var boards = await boardRepository.GetDeletedBoardsOrderedAsync();
        return boards.Select(board => new DeletedBoardSummaryDto(board.Id, board.Name, board.DeletedAtUtc!.Value))
            .ToList();
    }

    public async Task<ApiResult<SystemBoardSummaryDto>> RestoreBoardAsync(int boardId)
    {
        using var scope = scopeFactory.CreateWithTransaction(IsolationLevel.Serializable);

        var board = await boardRepository.GetIncludingDeletedAsync(boardId);
        if (board is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        if (board.DeletedAtUtc.HasValue)
        {
            board.DeletedAtUtc = null;
            await scope.SaveChangesAsync();
        }

        return ToSummary(board);
    }

    public async Task<ApiResult<JobDto>> QueueBoardPurgeAsync(int boardId, int actorUserId, CancellationToken cancellationToken)
    {
        using (scopeFactory.CreateReadOnly())
        {
            var board = await boardRepository.GetIncludingDeletedAsync(boardId, cancellationToken);
            if (board is null)
            {
                return ApiErrors.NotFound("Board not found.");
            }

            if (board.DeletedAtUtc is null)
            {
                return new ApiError(409, "Only deleted boards can be purged.");
            }
        }

        return await jobs.EnqueueAsync(new CreateJobRequest(
            PurgeBoardJobHandler.JobType,
            JsonSerializer.Serialize(new PurgeBoardJobPayload(boardId)),
            UserId: actorUserId), cancellationToken);
    }

    public async Task<ApiResult<IReadOnlyList<BoardMemberDto>>> GetMembersAsync(int boardId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var members = await boardMemberRepository.GetMembersInBoardAsync(boardId);
        return members.Select(x => x.ToDto()).ToList();
    }

    public async Task<ApiResult<BoardMemberDto>> AddMemberAsync(int boardId, AddBoardMemberRequest request)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        if (!TryParseBoardMemberRole(request.Role, out var role))
        {
            return ApiErrors.ValidationFailed([new ValidationError("role", "Role must be 'Owner' or 'Contributor'.")]);
        }

        var user = userRepository.Get(request.UserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.ValidationFailed([new ValidationError("userId", "Select an active user.")]);
        }

        var existingMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, request.UserId);
        if (existingMembership is not null)
        {
            return ApiErrors.ValidationFailed([new ValidationError("userId", "User is already a board member.")]);
        }

        var now = DateTime.UtcNow;
        var membership = new EntityBoardMember
        {
            BoardId = boardId,
            UserId = request.UserId,
            Role = role,
        };
        boardMemberRepository.Add(membership);
        await scope.SaveChangesAsync();

        var createdMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, request.UserId);
        if (createdMembership is null)
        {
            return ApiErrors.InternalError("Created board membership could not be reloaded.");
        }

        return ApiResults.Created(createdMembership.ToDto());
    }

    public async Task<ApiResult<BoardMemberDto>> UpdateMemberRoleAsync(int boardId, int userId, UpdateBoardMemberRoleRequest request)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        if (!TryParseBoardMemberRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Owner' or 'Contributor'.");
        }

        var existingMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, userId);
        if (existingMembership is null)
        {
            return ApiErrors.NotFound("Board member not found.");
        }

        if (existingMembership.Role == BoardMemberRole.Owner && role != BoardMemberRole.Owner)
        {
            var ownerCount = await boardMemberRepository.CountOwnersAsync(boardId);
            if (ownerCount <= 1)
            {
                return ApiErrors.BadRequest("Board must have at least one owner.");
            }
        }

        if (existingMembership.Role != role)
        {
            existingMembership.Role = role;
            await scope.SaveChangesAsync();
        }

        return existingMembership.ToDto();
    }

    public async Task<ApiResult> RemoveMemberAsync(int boardId, int userId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var existingMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, userId);
        if (existingMembership is null)
        {
            return ApiResults.Ok();
        }

        if (existingMembership.Role == BoardMemberRole.Owner)
        {
            var ownerCount = await boardMemberRepository.CountOwnersAsync(boardId);
            if (ownerCount <= 1)
            {
                return ApiErrors.BadRequest("Board must have at least one owner.");
            }
        }

        boardMemberRepository.Remove(existingMembership);
        await scope.SaveChangesAsync();
        return ApiResults.Ok();
    }

    private static SystemBoardSummaryDto ToSummary(EntityBoard board) =>
        new(board.Id, board.Name, board.CreatedAtUtc, board.UpdatedAtUtc);

    private static bool TryParseBoardMemberRole(string value, out BoardMemberRole role)
    {
        if (string.Equals(value, nameof(BoardMemberRole.Owner), StringComparison.OrdinalIgnoreCase))
        {
            role = BoardMemberRole.Owner;
            return true;
        }

        if (string.Equals(value, nameof(BoardMemberRole.Contributor), StringComparison.OrdinalIgnoreCase))
        {
            role = BoardMemberRole.Contributor;
            return true;
        }

        role = default;
        return false;
    }
}
