using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardCloneServiceTests : TestBaseDb
{
    [Fact]
    public async Task CloneBoardAsync_ShouldCopyConfigurationWithoutBoardContentOrMembers()
    {
        // Arrange
        var source = CreateBoard("Source board")
            .AddColumn("Backlog")
            .AddCard("Source card")
            .AddColumn("Released")
            .Build();
        var sourceBoard = DbContextForArrange.Boards.Single(x => x.Id == source.BoardId);
        sourceBoard.Description = "Source guidance";
        sourceBoard.SlickCohesionModeEnabled = false;

        var originalDefaultType = DbContextForArrange.CardTypes.Single(x => x.BoardId == source.BoardId && x.IsSystem);
        originalDefaultType.IsSystem = false;
        var featureType = new EntityCardType
        {
            BoardId = source.BoardId,
            Name = "Feature",
            Emoji = "🎬️",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto","borderMode":"auto"}""",
            IsSystem = true,
        };
        DbContextForArrange.CardTypes.Add(featureType);
        DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = source.BoardId,
            Name = "Priority",
            NormalisedName = "PRIORITY",
            Emoji = "🔥",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":3}""",
        });
        DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = source.BoardId,
            Name = "Release train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":2}""",
        });

        var additionalMember = new EntityUser
        {
            UserName = "additional-member",
            Email = "additional-member@example.com",
            NormalisedEmail = "additional-member@example.com",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(additionalMember);
        sourceBoard.Members.Add(new EntityBoardMember
        {
            User = additionalMember,
            Role = BoardMemberRole.Contributor,
        });
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = source.BoardId,
            OriginalCardId = 20,
            ArchivedAtUtc = new DateTime(2026, 8, 31, 8, 0, 0, DateTimeKind.Utc),
            SnapshotJson = """{"schema":"archived-card","version":1,"payload":{"title":"Archived source card"}}""",
            SearchTitle = "Archived source card",
            SearchTagsJson = "[]",
            SearchTextNormalised = "ARCHIVED SOURCE CARD",
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IBoardCloneService>();

        // Act
        var result = await service.CloneBoardAsync(
            source.BoardId,
            new CloneBoardRequest("  Cloned board  "),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Cloned board", result.Data!.Name);
        Assert.Equal("Source guidance", result.Data.Description);
        Assert.False(result.Data.SlickCohesionModeEnabled);
        Assert.Equal(["Backlog", "Released"], result.Data.Columns.Select(x => x.Title));
        Assert.All(result.Data.Columns, column => Assert.Empty(column.Cards));

        var clonedBoardId = result.Data.Id;
        var clonedCardTypes = await DbContextForAssert.CardTypes
            .Where(x => x.BoardId == clonedBoardId)
            .OrderBy(x => x.Name)
            .ToListAsync();
        Assert.Equal(["Feature", "Story"], clonedCardTypes.Select(x => x.Name));
        var clonedDefaultType = Assert.Single(clonedCardTypes.Where(x => x.IsSystem));
        Assert.Equal("Feature", clonedDefaultType.Name);
        Assert.Equal("🎬️", clonedDefaultType.Emoji);
        Assert.Equal("solid", clonedDefaultType.StyleName);
        Assert.Equal(featureType.StylePropertiesJson, clonedDefaultType.StylePropertiesJson);

        var clonedTag = Assert.Single(DbContextForAssert.Tags.Where(x => x.BoardId == clonedBoardId));
        Assert.Equal("Priority", clonedTag.Name);
        Assert.Equal("🔥", clonedTag.Emoji);
        Assert.Equal("presets", clonedTag.StyleName);
        Assert.Equal("""{"presetIndex":3}""", clonedTag.StylePropertiesJson);

        var clonedSlick = Assert.Single(DbContextForAssert.Slicks.Where(x => x.BoardId == clonedBoardId));
        Assert.Equal("Release train", clonedSlick.Name);
        Assert.Equal("presets", clonedSlick.StyleName);
        Assert.Equal("""{"presetIndex":2}""", clonedSlick.StylePropertiesJson);

        Assert.Empty(DbContextForAssert.Cards.Where(x => x.BoardId == clonedBoardId));
        Assert.Empty(DbContextForAssert.ArchivedCards.Where(x => x.BoardId == clonedBoardId));
        var clonedMembership = Assert.Single(DbContextForAssert.BoardMembers.Where(x => x.BoardId == clonedBoardId));
        Assert.Equal(ActorUserId, clonedMembership.UserId);
        Assert.Equal(BoardMemberRole.Owner, clonedMembership.Role);
        var clonedSequence = DbContextForAssert.BoardCardIdSequences.Single(x => x.BoardId == clonedBoardId);
        Assert.Equal(1, clonedSequence.NextCardId);
    }

    [Fact]
    public async Task CloneBoardAsync_WhenActorIsContributor_ShouldCreateOwnedClone()
    {
        // Arrange
        var source = CreateBoard("Source board")
            .AddColumn("Todo")
            .Build();
        var contributor = new EntityUser
        {
            UserName = "contributor",
            Email = "contributor@example.com",
            NormalisedEmail = "contributor@example.com",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(contributor);
        DbContextForArrange.BoardMembers.Add(new EntityBoardMember
        {
            BoardId = source.BoardId,
            User = contributor,
            Role = BoardMemberRole.Contributor,
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IBoardCloneService>();

        // Act
        var result = await service.CloneBoardAsync(
            source.BoardId,
            new CloneBoardRequest("Contributor clone"),
            contributor.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var clonedMembership = Assert.Single(
            DbContextForAssert.BoardMembers.Where(x => x.BoardId == result.Data!.Id));
        Assert.Equal(contributor.Id, clonedMembership.UserId);
        Assert.Equal(BoardMemberRole.Owner, clonedMembership.Role);
    }

    [Fact]
    public async Task CloneBoardAsync_WhenActorCannotAccessSource_ShouldReturnForbiddenWithoutCreatingBoard()
    {
        // Arrange
        var source = CreateBoard("Source board").Build();
        var nonMember = new EntityUser
        {
            UserName = "non-member",
            Email = "non-member@example.com",
            NormalisedEmail = "non-member@example.com",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(nonMember);
        await DbContextForArrange.SaveChangesAsync();
        var boardCountBefore = DbContextForArrange.Boards.Count();
        var service = ResolveService<IBoardCloneService>();

        // Act
        var result = await service.CloneBoardAsync(
            source.BoardId,
            new CloneBoardRequest("Forbidden clone"),
            nonMember.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(boardCountBefore, DbContextForAssert.Boards.Count());
    }
}
