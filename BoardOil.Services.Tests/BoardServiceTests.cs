using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.Column;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardServiceTests : TestBaseDb
{
    [Fact]
    public async Task UpdateBoardAsync_WhenBoardDoesNotExist_ShouldReturnNotFound()
    {
        // Act
        var service = CreateService();
        var result = await service.UpdateBoardAsync(999, new UpdateBoardRequest("Renamed", true), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task UpdateBoardAsync_ShouldTrimAndPersistName()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .Build();
        var service = CreateService();

        // Act
        var result = await service.UpdateBoardAsync(board.BoardId, new UpdateBoardRequest("  Roadmap  ", true), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(board.BoardId, result.Data!.Id);
        Assert.Equal("Roadmap", result.Data.Name);
        Assert.Equal(string.Empty, result.Data.Description);
        Assert.True(result.Data.SlickCohesionModeEnabled);

        var persisted = DbContextForAssert.Boards.Single(x => x.Id == board.BoardId);
        Assert.Equal("Roadmap", persisted.Name);
        Assert.Equal(string.Empty, persisted.Description);
    }

    [Fact]
    public async Task UpdateBoardAsync_ShouldTrimAndPersistDescription()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .Build();
        var service = CreateService();

        // Act
        var result = await service.UpdateBoardAsync(
            board.BoardId,
            new UpdateBoardRequest("BoardOil", true, "  Service-level board guidance  "),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Service-level board guidance", result.Data!.Description);
        Assert.True(result.Data.SlickCohesionModeEnabled);

        var persisted = DbContextForAssert.Boards.Single(x => x.Id == board.BoardId);
        Assert.Equal("Service-level board guidance", persisted.Description);
    }

    [Fact]
    public async Task UpdateBoardAsync_ShouldPersistSlickCohesionMode()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .Build();
        var service = CreateService();

        // Act
        var result = await service.UpdateBoardAsync(
            board.BoardId,
            new UpdateBoardRequest("BoardOil", false, "Board description"),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.SlickCohesionModeEnabled);

        var persisted = DbContextForAssert.Boards.Single(x => x.Id == board.BoardId);
        Assert.False(persisted.SlickCohesionModeEnabled);
    }

    [Fact]
    public async Task UpdateBoardAsync_ShouldPersistCardAttachmentThumbnailSettingAndPreserveItWhenOmitted()
    {
        var board = CreateBoard("BoardOil").Build();
        var service = CreateService();

        var disabled = await service.UpdateBoardAsync(
            board.BoardId,
            new UpdateBoardRequest("BoardOil", true, CardAttachmentThumbnailsEnabled: false),
            ActorUserId);
        var omitted = await service.UpdateBoardAsync(
            board.BoardId,
            new UpdateBoardRequest("Renamed", true),
            ActorUserId);

        Assert.True(disabled.Success);
        Assert.False(disabled.Data!.CardAttachmentThumbnailsEnabled);
        Assert.True(omitted.Success);
        Assert.False(omitted.Data!.CardAttachmentThumbnailsEnabled);
        Assert.False(DbContextForAssert.Boards.Single(x => x.Id == board.BoardId).CardAttachmentThumbnailsEnabled);
    }

    [Fact]
    public async Task CreateBoardAsync_ShouldDefaultSlickCohesionModeEnabledToTrue()
    {
        // Act
        var service = CreateService();
        var result = await service.CreateBoardAsync(new CreateBoardRequest("Roadmap"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.SlickCohesionModeEnabled);
        Assert.True(result.Data.CardAttachmentThumbnailsEnabled);

        var persisted = DbContextForAssert.Boards.Single(x => x.Id == result.Data.Id);
        Assert.True(persisted.SlickCohesionModeEnabled);
        Assert.True(persisted.CardAttachmentThumbnailsEnabled);
        var sequence = DbContextForAssert.BoardCardIdSequences.Single(x => x.BoardId == result.Data.Id);
        Assert.Equal(1, sequence.NextCardId);
    }

    [Fact]
    public async Task DeleteBoardAsync_ShouldRetainBoardAndItsContents()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A")
            .Build();
        var columnId = board.GetColumn("Todo").Id;
        var cardId = board.GetCard("Todo", "Card A").Id;
        var service = CreateService();

        // Act
        var result = await service.DeleteBoardAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        using (ResolveService<IDbContextScopeFactory>().CreateReadOnly())
        {
            Assert.Null(ResolveService<IBoardRepository>().Get(board.BoardId));
        }
        Assert.NotNull(DbContextForAssert.Boards.Single(x => x.Id == board.BoardId).DeletedAtUtc);
        Assert.Contains(DbContextForAssert.Columns, x => x.Id == columnId);
        Assert.Contains(DbContextForAssert.Cards, x => x.Id == cardId);
        Assert.Contains(DbContextForAssert.BoardMembers, x => x.BoardId == board.BoardId && x.UserId == ActorUserId);
        Assert.Contains(DbContextForAssert.BoardCardIdSequences, x => x.BoardId == board.BoardId);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(board.BoardId, Assert.Single(events.ResyncRequestedBoardIds));
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenContributor_ShouldLeaveBoardActive()
    {
        var board = CreateBoard().Build();
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = BoardMemberRole.Contributor;
        await DbContextForArrange.SaveChangesAsync();

        var result = await CreateService().DeleteBoardAsync(board.BoardId, ActorUserId);

        Assert.Equal(403, result.StatusCode);
        Assert.Null(DbContextForAssert.Boards.Single(x => x.Id == board.BoardId).DeletedAtUtc);
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenAlreadyDeleted_ShouldKeepDeletionTime()
    {
        var board = CreateBoard().Build();
        var deletedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        DbContextForArrange.Boards.Single(x => x.Id == board.BoardId).DeletedAtUtc = deletedAt;
        await DbContextForArrange.SaveChangesAsync();

        var result = await CreateService().DeleteBoardAsync(board.BoardId, ActorUserId);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(deletedAt, DbContextForAssert.Boards.Single(x => x.Id == board.BoardId).DeletedAtUtc);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task EnsureDefaultBoardAsync_WhenOnlyBoardIsDeleted_ShouldNotSeedReplacement()
    {
        var board = CreateBoard().Build();
        Assert.True((await CreateService().DeleteBoardAsync(board.BoardId, ActorUserId)).Success);

        await ResolveService<IBoardBootstrapService>().EnsureDefaultBoardAsync();

        Assert.Equal(board.BoardId, Assert.Single(DbContextForAssert.Boards).Id);
    }

    [Fact]
    public async Task DeletedBoard_ShouldBeUnavailableToOwnerAndSystemBoardManagement()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardNumber = board.GetCard("Card").BoardCardId;
        var service = CreateService();
        Assert.True((await service.DeleteBoardAsync(board.BoardId, ActorUserId)).Success);

        var boards = await service.GetBoardsAsync(ActorUserId);
        var loaded = await service.GetBoardAsync(board.BoardId, ActorUserId);
        var card = await ResolveService<ICardService>().GetCardAsync(board.BoardId, cardNumber, ActorUserId);
        var systemBoards = ResolveService<ISystemBoardService>();
        var systemList = await systemBoards.GetBoardsAsync();
        var memberChange = await systemBoards.RemoveMemberAsync(board.BoardId, ActorUserId);

        Assert.Empty(boards.Data!);
        Assert.Equal(404, loaded.StatusCode);
        Assert.Equal(403, card.StatusCode);
        Assert.Empty(systemList.Data!);
        Assert.Equal(404, memberChange.StatusCode);
    }

    [Fact]
    public async Task DeleteBoardAsync_WhenBoardDoesNotExist_ShouldReturnOk()
    {
        // Act
        var service = CreateService();
        var result = await service.DeleteBoardAsync(999, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task GetBoardAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync(1, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task GetBoardAsync_WhenBoardHasNoColumns_ShouldReturnEmptyColumns()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .Build();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(board.BoardId, result.Data!.Id);
        Assert.Equal("BoardOil", result.Data.Name);
        Assert.Empty(result.Data.Columns);
    }

    [Fact]
    public async Task GetBoardAsync_WhenBoardHasColumnsAndCards_ShouldReturnOrderedHierarchy()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A")
            .AddCard("C")
            .AddCard("B", position: 1)
            .AddColumn("Doing")
            .AddCard("Done")
            .AddCard("In Progress", position: 0)
            .Build();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("BoardOil", result.Data!.Name);
        Assert.Equal(2, result.Data.Columns.Count);

        var todo = result.Data.Columns[0];
        Assert.Equal("Todo", todo.Title);
        Assert.False(string.IsNullOrWhiteSpace(todo.SortKey));
        Assert.Equal(3, todo.Cards.Count);
        Assert.Equal("A", todo.Cards[0].Title);
        Assert.Equal("B", todo.Cards[1].Title);
        Assert.Equal("C", todo.Cards[2].Title);
        Assert.False(string.IsNullOrWhiteSpace(todo.Cards[0].SortKey));
        Assert.False(string.IsNullOrWhiteSpace(todo.Cards[1].SortKey));
        Assert.False(string.IsNullOrWhiteSpace(todo.Cards[2].SortKey));

        var doing = result.Data.Columns[1];
        Assert.Equal("Doing", doing.Title);
        Assert.False(string.IsNullOrWhiteSpace(doing.SortKey));
        Assert.Equal(2, doing.Cards.Count);
        Assert.Equal("In Progress", doing.Cards[0].Title);
        Assert.Equal("Done", doing.Cards[1].Title);
        Assert.False(string.IsNullOrWhiteSpace(doing.Cards[0].SortKey));
        Assert.False(string.IsNullOrWhiteSpace(doing.Cards[1].SortKey));
    }

    [Fact]
    public async Task GetBoardAsync_WhenCardsHaveTags_ShouldIncludeTagNamesAndRichTags()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "A").Id;
        var now = DateTime.UtcNow;
        var tag = new EntityTag
        {
            BoardId = board.BoardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
        };
        DbContextForArrange.Tags.Add(tag);
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.CardTags.Add(new EntityCardTag
        {
            CardId = cardId,
            TagId = tag.Id
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var todoCard = result.Data!.Columns[0].Cards[0];
        var richTag = Assert.Single(todoCard.Tags);
        Assert.Equal(tag.Id, richTag.Id);
        Assert.Equal("Bug", richTag.Name);
        Assert.Equal("solid", richTag.StyleName);
        Assert.Equal("""{"backgroundColor":"#224466","textColorMode":"auto"}""", richTag.StylePropertiesJson);
        Assert.Null(richTag.Emoji);
        Assert.Equal(["Bug"], todoCard.TagNames);
    }

    [Fact]
    public async Task GetBoardAsync_WhenAssignedUserHasProfileImage_ShouldIncludeAssignedUserImageRelativePath()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Assigned card")
            .Build();
        var cardId = board.GetCard("Todo", "Assigned card").Id;
        var now = DateTime.UtcNow;

        var card = DbContextForArrange.Cards.Single(x => x.Id == cardId);
        card.AssignedUserId = ActorUserId;
        DbContextForArrange.Images.Add(new EntityImage
        {
            EntityType = ImageEntityType.UserProfile,
            EntityId = ActorUserId,
            OriginalFileName = "avatar.png",
            ContentType = "image/png",
            RelativePath = "userprofile/1/avatar.png",
            ByteLength = 1234,
            Width = 256,
            Height = 256,
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync(board.BoardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var todoCard = result.Data!.Columns[0].Cards[0];
        Assert.Equal(ActorUserId, todoCard.AssignedUserId);
        Assert.Equal("userprofile/1/avatar.png", todoCard.AssignedUserImageRelativePath);
    }

    [Fact]
    public async Task GetBoardsAsync_ShouldReturnOnlyActorMemberships()
    {
        // Arrange
        var actorBoard = CreateBoard("Actor Board")
            .Build();
        await CreateForeignBoardAsync("Foreign Board");
        var service = CreateService();

        // Act
        var result = await service.GetBoardsAsync(ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data!, x => x.Id == actorBoard.BoardId && x.Name == "Actor Board");
        Assert.DoesNotContain(result.Data!, x => x.Name == "Foreign Board");
    }

    private BoardService CreateService()
    {
        return ResolveService<BoardService>();
    }

    private async Task CreateForeignBoardAsync(string boardName)
    {
        var now = DateTime.UtcNow;
        var ownerUserName = $"owner-{Guid.NewGuid():N}";
        var ownerEmail = $"{ownerUserName}@localhost";
        var owner = new EntityUser
        {
            UserName = ownerUserName,
            Email = ownerEmail,
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(owner);
        await DbContextForArrange.SaveChangesAsync();

        var board = new EntityBoard
        {
            Name = boardName,
        };
        board.Members.Add(new EntityBoardMember
        {
            UserId = owner.Id,
            Role = BoardMemberRole.Owner,
        });
        DbContextForArrange.Boards.Add(board);
        await DbContextForArrange.SaveChangesAsync();
    }
}
