using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SystemBoardServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetBoardsAsync_ShouldReturnAllBoards()
    {
        // Arrange
        CreateBoard("Actor Board").Build();
        await CreateForeignBoardAsync("Foreign Board");
        var service = CreateService();

        // Act
        var result = await service.GetBoardsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data!, x => x.Name == "Actor Board");
        Assert.Contains(result.Data!, x => x.Name == "Foreign Board");
    }

    [Fact]
    public async Task AddMemberAsync_WhenActorIsNotBoardMember_ShouldStillAddMembership()
    {
        // Arrange
        var (boardId, _) = await CreateForeignBoardAsync("Backstop Board");
        var targetUserId = await CreateUserAsync("member-target", UserRole.Standard);
        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(boardId, new AddBoardMemberRequest(targetUserId, "Contributor"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(targetUserId, result.Data!.UserId);
        Assert.Equal("Contributor", result.Data.Role);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenDemotingLastOwner_ShouldReturnBadRequest()
    {
        // Arrange
        var (boardId, ownerUserId) = await CreateForeignBoardAsync("Owner Guard Board");
        var service = CreateService();

        // Act
        var result = await service.UpdateMemberRoleAsync(boardId, ownerUserId, new UpdateBoardMemberRoleRequest("Contributor"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Board must have at least one owner.", result.Message);
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserAlreadyMember_ShouldReturnBadRequest()
    {
        // Arrange
        var (boardId, _) = await CreateForeignBoardAsync("Duplicate Member Board");
        var userId = await CreateUserAsync("duplicate-user", UserRole.Standard);
        var service = CreateService();

        var added = await service.AddMemberAsync(boardId, new AddBoardMemberRequest(userId, "Contributor"));
        Assert.True(added.Success);

        // Act
        var result = await service.AddMemberAsync(boardId, new AddBoardMemberRequest(userId, "Owner"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("User is already a board member.", result.Message);
    }

    private ISystemBoardService CreateService()
    {
        return ResolveService<ISystemBoardService>();
    }

    private async Task<int> CreateUserAsync(string userNamePrefix, UserRole role, bool isActive = true)
    {
        var now = DateTime.UtcNow;
        var user = new EntityUser
        {
            UserName = $"{userNamePrefix}-{Guid.NewGuid():N}",
            PasswordHash = "test-hash",
            Role = role,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(int BoardId, int OwnerUserId)> CreateForeignBoardAsync(string boardName)
    {
        var now = DateTime.UtcNow;
        var ownerUserId = await CreateUserAsync("board-owner", UserRole.Standard);

        var board = new EntityBoard
        {
            Name = boardName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        board.Members.Add(new EntityBoardMember
        {
            UserId = ownerUserId,
            Role = BoardMemberRole.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        DbContextForArrange.Boards.Add(board);
        await DbContextForArrange.SaveChangesAsync();
        return (board.Id, ownerUserId);
    }
}
