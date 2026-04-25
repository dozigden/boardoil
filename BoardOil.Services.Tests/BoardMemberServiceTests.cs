using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardMemberServiceTests : TestBaseDb
{
    [Fact]
    public async Task AddMemberAsync_WhenValid_ShouldCreateBoardMembership()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var member = await AddUserAsync("member");
        var service = ResolveService<IBoardMemberService>();

        // Act
        var result = await service.AddMemberAsync(board.BoardId, new AddBoardMemberRequest(member.Id, "Contributor"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(member.Id, result.Data!.UserId);
        Assert.Equal(BoardMemberRole.Contributor.ToString(), result.Data.Role);

        var stored = await DbContextForAssert.BoardMembers.SingleAsync(x => x.BoardId == board.BoardId && x.UserId == member.Id);
        Assert.Equal(BoardMemberRole.Contributor, stored.Role);
    }

    [Fact]
    public async Task AddMemberAsync_WhenMembershipAlreadyExists_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var member = await AddUserAsync("member");
        var service = ResolveService<IBoardMemberService>();
        _ = await service.AddMemberAsync(board.BoardId, new AddBoardMemberRequest(member.Id, "Contributor"), ActorUserId);

        // Act
        var result = await service.AddMemberAsync(board.BoardId, new AddBoardMemberRequest(member.Id, "Contributor"), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("User is already a board member.", result.Message);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenDemotingLastOwner_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<IBoardMemberService>();

        // Act
        var result = await service.UpdateMemberRoleAsync(
            board.BoardId,
            ActorUserId,
            new UpdateBoardMemberRoleRequest("Contributor"),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Board must have at least one owner.", result.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRemovingLastOwner_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<IBoardMemberService>();

        // Act
        var result = await service.RemoveMemberAsync(board.BoardId, ActorUserId, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Board must have at least one owner.", result.Message);
    }

    private async Task<EntityUser> AddUserAsync(string userName)
    {
        var now = DateTime.UtcNow;
        var user = new EntityUser
        {
            UserName = userName,
            Email = $"{userName}@localhost",
            NormalisedEmail = $"{userName}@localhost",
            PasswordHash = "hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.User,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        return user;
    }
}
