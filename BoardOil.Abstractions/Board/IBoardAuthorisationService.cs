namespace BoardOil.Abstractions.Board;

public interface IBoardAuthorisationService
{
    Task<bool> HasPermissionAsync(int boardId, int actorUserId, BoardPermission permission);
}
