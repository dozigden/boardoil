using BoardOil.Contracts.CardType;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.CardType;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardTypeServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetCardTypesAsync_WhenBoardMemberHasAccess_ShouldReturnBoardScopedTypes()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var now = DateTime.UtcNow;
        DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Bug",
            Emoji = "🐞",
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await DbContextForArrange.SaveChangesAsync();
        var contributorUserId = await AddContributorAsync(boardId, "contributor-tags");
        var service = CreateService();

        // Act
        var result = await service.GetCardTypesAsync(boardId, contributorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Story", "Bug"], result.Data!.Select(x => x.Name).ToArray());
        Assert.Equal([true, false], result.Data.Select(x => x.IsSystem).ToArray());
    }

    [Fact]
    public async Task CreateCardTypeAsync_WhenActorIsContributor_ShouldReturnForbidden()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var contributorUserId = await AddContributorAsync(boardId, "create-contributor");
        var service = CreateService();

        // Act
        var result = await service.CreateCardTypeAsync(boardId, new CreateCardTypeRequest("Feature"), contributorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("You do not have permission for this action.", result.Message);
    }

    [Fact]
    public async Task CreateCardTypeAsync_WhenNameConflictsCaseInsensitive_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var now = DateTime.UtcNow;
        DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Feature",
            Emoji = null,
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.CreateCardTypeAsync(boardId, new CreateCardTypeRequest("  feature  "), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("name"));
    }

    [Fact]
    public async Task CreateCardTypeAsync_WhenStyleFieldsOmitted_ShouldUseDefaultStyle()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateCardTypeAsync(boardId, new CreateCardTypeRequest("Feature"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("solid", result.Data!.StyleName);
        Assert.Equal("""{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""", result.Data.StylePropertiesJson);

        var stored = await DbContextForAssert.CardTypes.SingleAsync(x => x.Id == result.Data.Id);
        Assert.Equal("solid", stored.StyleName);
        Assert.Equal("""{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""", stored.StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateCardTypeAsync_WhenSystemType_ShouldAllowRenameEmojiAndStyleUpdate()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var systemType = await DbContextForArrange.CardTypes.SingleAsync(x => x.BoardId == boardId && x.IsSystem);
        var service = CreateService();

        // Act
        var result = await service.UpdateCardTypeAsync(
            boardId,
            systemType.Id,
            new UpdateCardTypeRequest(
                "Epic",
                "🚀",
                "gradient",
                """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}"""),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Epic", result.Data!.Name);
        Assert.Equal("🚀", result.Data.Emoji);
        Assert.Equal("gradient", result.Data.StyleName);
        Assert.Equal("""{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""", result.Data.StylePropertiesJson);
        Assert.True(result.Data.IsSystem);

        var stored = await DbContextForAssert.CardTypes.SingleAsync(x => x.Id == systemType.Id);
        Assert.Equal("Epic", stored.Name);
        Assert.Equal("🚀", stored.Emoji);
        Assert.Equal("gradient", stored.StyleName);
        Assert.Equal("""{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""", stored.StylePropertiesJson);
        Assert.True(stored.IsSystem);
    }

    [Fact]
    public async Task UpdateCardTypeAsync_WhenActorIsContributor_ShouldReturnForbidden()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var now = DateTime.UtcNow;
        var cardType = new EntityCardType
        {
            BoardId = boardId,
            Name = "Feature",
            Emoji = null,
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.CardTypes.Add(cardType);
        await DbContextForArrange.SaveChangesAsync();
        var contributorUserId = await AddContributorAsync(boardId, "update-contributor");
        var service = CreateService();

        // Act
        var result = await service.UpdateCardTypeAsync(boardId, cardType.Id, new UpdateCardTypeRequest("Feature+"), contributorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task DeleteCardTypeAsync_WhenSystemType_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var systemType = await DbContextForArrange.CardTypes.SingleAsync(x => x.BoardId == boardId && x.IsSystem);
        var service = CreateService();

        // Act
        var result = await service.DeleteCardTypeAsync(boardId, systemType.Id, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("System card type cannot be deleted.", result.Message);
    }

    [Fact]
    public async Task DeleteCardTypeAsync_WhenActorIsContributor_ShouldReturnForbidden()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var now = DateTime.UtcNow;
        var cardType = new EntityCardType
        {
            BoardId = boardId,
            Name = "Feature",
            Emoji = null,
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.CardTypes.Add(cardType);
        await DbContextForArrange.SaveChangesAsync();
        var contributorUserId = await AddContributorAsync(boardId, "delete-contributor");
        var service = CreateService();

        // Act
        var result = await service.DeleteCardTypeAsync(boardId, cardType.Id, contributorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task DeleteCardTypeAsync_WhenNonSystemTypeExists_ShouldReassignCardsToSystemType()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Task A")
            .Build();
        var boardId = board.BoardId;
        var cardId = board.GetCard("Todo", "Task A").Id;
        var systemType = await DbContextForArrange.CardTypes.SingleAsync(x => x.BoardId == boardId && x.IsSystem);
        var now = DateTime.UtcNow;
        var bugType = new EntityCardType
        {
            BoardId = boardId,
            Name = "Bug",
            Emoji = "🐞",
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.CardTypes.Add(bugType);
        await DbContextForArrange.SaveChangesAsync();

        var card = await DbContextForArrange.Cards.SingleAsync(x => x.Id == cardId);
        card.CardTypeId = bugType.Id;
        card.UpdatedAtUtc = now;
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.DeleteCardTypeAsync(boardId, bugType.Id, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var storedCard = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Equal(systemType.Id, storedCard.CardTypeId);
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
        Assert.DoesNotContain(await DbContextForAssert.CardTypes.ToListAsync(), x => x.Id == bugType.Id);
    }

    private CardTypeService CreateService() =>
        ResolveService<CardTypeService>();

    private async Task<int> AddContributorAsync(int boardId, string userName)
    {
        var now = DateTime.UtcNow;
        var user = new EntityUser
        {
            UserName = userName,
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();

        DbContextForArrange.BoardMembers.Add(new EntityBoardMember
        {
            BoardId = boardId,
            UserId = user.Id,
            Role = BoardMemberRole.Contributor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await DbContextForArrange.SaveChangesAsync();

        return user.Id;
    }
}
