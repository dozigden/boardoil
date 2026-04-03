using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class BoardMemberRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardMember>(ambientDbContextLocator), IBoardMemberRepository
{
    public async Task<IReadOnlyList<EntityBoardMember>> GetMembershipsForUserOrderedAsync(int userId) =>
        await DbSet
            .Where(x => x.UserId == userId)
            .Include(x => x.Board)
            .OrderBy(x => x.BoardId)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoardMember>> GetMembersInBoardAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .Include(x => x.User)
            .OrderBy(x => x.User.UserName)
            .ToListAsync();

    public Task<EntityBoardMember?> GetByBoardAndUserAsync(int boardId, int userId) =>
        DbSet
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.BoardId == boardId && x.UserId == userId);

    public Task<int> CountOwnersAsync(int boardId) =>
        DbSet.CountAsync(x => x.BoardId == boardId && x.Role == BoardMemberRole.Owner);
}
