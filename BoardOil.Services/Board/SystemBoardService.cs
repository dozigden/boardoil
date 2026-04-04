using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Users;

namespace BoardOil.Services.Board;

public sealed class SystemBoardService(
    IBoardRepository boardRepository,
    IBoardMemberRepository boardMemberRepository,
    IUserRepository userRepository,
    IDbContextScopeFactory scopeFactory) : ISystemBoardService
{
    public async Task<ApiResult<IReadOnlyList<SystemBoardSummaryDto>>> GetBoardsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var boards = await boardRepository.GetBoardsOrderedAsync();
        return boards.Select(x => new SystemBoardSummaryDto(
            x.Id,
            x.Name,
            x.CreatedAtUtc,
            x.UpdatedAtUtc))
            .ToList();
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
            return ApiErrors.BadRequest("Role must be 'Owner' or 'Contributor'.");
        }

        var user = userRepository.Get(request.UserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.NotFound("User not found.");
        }

        var existingMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, request.UserId);
        if (existingMembership is not null)
        {
            return ApiErrors.BadRequest("User is already a board member.");
        }

        var now = DateTime.UtcNow;
        var membership = new EntityBoardMember
        {
            BoardId = boardId,
            UserId = request.UserId,
            Role = role,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            existingMembership.UpdatedAtUtc = DateTime.UtcNow;
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
