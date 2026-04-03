using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Board;

public sealed class BoardAuthorisationService(
    IBoardMemberRepository boardMemberRepository,
    IDbContextScopeFactory scopeFactory) : IBoardAuthorisationService
{
    private static readonly ISet<BoardPermission> ContributorPermissions = new HashSet<BoardPermission>
    {
        BoardPermission.BoardAccess,
        BoardPermission.CardCreate,
        BoardPermission.CardUpdate,
        BoardPermission.CardDelete,
        BoardPermission.CardMove,
        BoardPermission.TagManage
    };

    public async Task<bool> HasPermissionAsync(int boardId, int actorUserId, BoardPermission permission)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, actorUserId);
        if (membership is null || !membership.User.IsActive)
        {
            return false;
        }

        if (membership.Role == BoardMemberRole.Owner)
        {
            return true;
        }

        if (ContributorPermissions.Contains(permission))
        {
            return true;
        }

        return false;
    }
}
