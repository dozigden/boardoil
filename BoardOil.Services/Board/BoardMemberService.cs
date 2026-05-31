using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Users;

namespace BoardOil.Services.Board;

public sealed class BoardMemberService(
    IBoardRepository boardRepository,
    IBoardMemberRepository boardMemberRepository,
    IImageRepository imageRepository,
    IUserRepository userRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IDbContextScopeFactory scopeFactory) : IBoardMemberService
{
    public async Task<ApiResult<IReadOnlyList<BoardMemberDto>>> GetMembersAsync(int boardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var members = await boardMemberRepository.GetMembersInBoardAsync(boardId);
        var memberUserIds = members.Select(x => x.UserId).Distinct().ToArray();
        var userImages = await imageRepository.GetLatestForEntitiesAsync(ImageEntityType.UserProfile, memberUserIds);
        var imageLookup = userImages.ToDictionary(x => x.EntityId, x => x.RelativePath);
        return members.Select(x => x.ToDto(imageLookup)).ToList();
    }

    public async Task<ApiResult<BoardMemberDto>> AddMemberAsync(int boardId, AddBoardMemberRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageMembers);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
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
        };
        boardMemberRepository.Add(membership);
        await scope.SaveChangesAsync();

        var createdMembership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, request.UserId);
        if (createdMembership is null)
        {
            return ApiErrors.InternalError("Created board membership could not be reloaded.");
        }

        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, createdMembership.UserId);
        return ApiResults.Created(createdMembership.ToDto(image?.RelativePath));
    }

    public async Task<ApiResult<BoardMemberDto>> UpdateMemberRoleAsync(int boardId, int userId, UpdateBoardMemberRoleRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageMembers);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
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

        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, existingMembership.UserId);
        return existingMembership.ToDto(image?.RelativePath);
    }

    public async Task<ApiResult> RemoveMemberAsync(int boardId, int userId, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageMembers);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
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

internal static class BoardMemberMappingExtensions
{
    public static BoardMemberDto ToDto(this EntityBoardMember member) =>
        ToDto(member, profileImageRelativePath: null);

    public static BoardMemberDto ToDto(this EntityBoardMember member, IReadOnlyDictionary<int, string> imageLookup) =>
        ToDto(member, imageLookup.GetValueOrDefault(member.UserId));

    public static BoardMemberDto ToDto(this EntityBoardMember member, string? profileImageRelativePath) =>
        new(
            member.UserId,
            member.User.UserName,
            member.User.DisplayName,
            profileImageRelativePath,
            member.Role.ToString(),
            member.CreatedAtUtc,
            member.UpdatedAtUtc);
}
