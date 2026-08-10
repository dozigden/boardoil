using BoardOil.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardOptionsServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetOptionsAsync_ShouldReturnUnusedOptionsAndOnlyActiveMembers()
    {
        // Arrange
        var board = CreateBoard("Options")
            .AddColumn("Todo")
            .Build();
        var activeMember = CreateUser("active-member", isActive: true);
        var inactiveMember = CreateUser("inactive-member", isActive: false);
        DbContextForArrange.Users.AddRange(activeMember, inactiveMember);
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.BoardMembers.AddRange(
            new EntityBoardMember
            {
                BoardId = board.BoardId,
                UserId = activeMember.Id,
                Role = BoardMemberRole.Contributor,
            },
            new EntityBoardMember
            {
                BoardId = board.BoardId,
                UserId = inactiveMember.Id,
                Role = BoardMemberRole.Contributor,
            });
        DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = board.BoardId,
            Name = "Bug",
            Emoji = "🕷️",
            StyleName = "solid",
            StylePropertiesJson = "{}",
            IsSystem = false,
        });
        DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = board.BoardId,
            Name = "Feature",
            NormalisedName = "FEATURE",
            Emoji = "🎬️",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        });
        DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = board.BoardId,
            Name = "Release Train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardOptionsService>();

        // Act
        var result = await service.GetOptionsAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data!.Columns, column => column.Title == "Todo");
        Assert.Contains(result.Data.Members, member => member.UserId == ActorUserId && member.Role == "Owner");
        Assert.Contains(result.Data.Members, member => member.UserId == activeMember.Id);
        Assert.DoesNotContain(result.Data.Members, member => member.UserId == inactiveMember.Id);
        Assert.Contains(result.Data.CardTypes, cardType => cardType.Name == "Bug");
        Assert.Contains(result.Data.CardTypes, cardType => cardType.Id == result.Data.DefaultCardTypeId && cardType.IsSystem);
        Assert.Contains(result.Data.Tags, tag => tag.Name == "Feature");
        Assert.Contains(result.Data.Slicks, slick => slick.Name == "Release Train");
    }

    [Fact]
    public async Task GetOptionsAsync_WhenActorDoesNotHaveBoardAccess_ShouldReturnForbidden()
    {
        // Arrange
        var board = CreateBoard("Options")
            .AddColumn("Todo")
            .Build();
        var outsider = CreateUser("outsider", isActive: true);
        DbContextForArrange.Users.Add(outsider);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardOptionsService>();

        // Act
        var result = await service.GetOptionsAsync(board.BoardId, outsider.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    private static EntityUser CreateUser(string userName, bool isActive) =>
        new()
        {
            UserName = userName,
            DisplayName = userName,
            Email = $"{userName}@localhost",
            NormalisedEmail = $"{userName.ToUpperInvariant()}@LOCALHOST",
            PasswordHash = "hash",
            Role = UserRole.Standard,
            IsActive = isActive,
        };
}
