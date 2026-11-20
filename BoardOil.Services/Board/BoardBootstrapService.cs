using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Board;

public sealed class BoardBootstrapService(
    IBoardRepository boardRepository,
    IUserRepository userRepository,
    IColumnRepository columnRepository,
    IAppSettingRepository settings,
    IDbContextScopeFactory scopeFactory) : IBoardBootstrapService
{
    private const string InitialisedKey = "default_board_initialised";

    public async Task EnsureDefaultBoardAsync()
    {
        using var scope = scopeFactory.Create();
        if (await settings.GetByKeyAsync(InitialisedKey) is not null)
        {
            return;
        }

        settings.Add(new EntityAppSetting { Key = InitialisedKey, Value = "true" });
        if (await boardRepository.AnyBoardIncludingDeletedAsync())
        {
            await scope.SaveChangesAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = "BoardOil",
            CardIdSequence = new EntityBoardCardIdSequence(),
        };

        var activeUsers = (await userRepository.GetUsersOrderedAsync())
            .Where(x => x.IsActive && x.IdentityType == UserIdentityType.User)
            .ToList();
        foreach (var user in activeUsers)
        {
            board.Members.Add(new EntityBoardMember
            {
                UserId = user.Id,
                Role = BoardMemberRole.Owner,
            });
        }

        boardRepository.Add(board);
        board.CardTypes.Add(CardTypeDefaults.CreateSystemForBoard(board, now));

        var seedTitles = new[] { "Todo", "In Progress", "Done" };
        string? previousSortKey = null;
        foreach (var title in seedTitles)
        {
            var sortKey = SortKeyGenerator.Between(previousSortKey, null);
            columnRepository.Add(new EntityBoardColumn
            {
                Board = board,
                Title = title,
                SortKey = sortKey,
            });
            previousSortKey = sortKey;
        }

        await scope.SaveChangesAsync();
    }
}
