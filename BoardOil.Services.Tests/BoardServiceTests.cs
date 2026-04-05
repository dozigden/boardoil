using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Ef.Repositories;
using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.Column;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.Persistence.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardServiceTests : TestBaseDb
{
    [Fact]
    public async Task UpdateBoardAsync_WhenBoardDoesNotExist_ShouldReturnNotFound()
    {
        // Act
        var service = CreateService();
        var result = await service.UpdateBoardAsync(999, new UpdateBoardRequest("Renamed"), ActorUserId);

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
        var result = await service.UpdateBoardAsync(board.BoardId, new UpdateBoardRequest("  Roadmap  "), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(board.BoardId, result.Data!.Id);
        Assert.Equal("Roadmap", result.Data.Name);

        var persisted = DbContextForAssert.Boards.Single(x => x.Id == board.BoardId);
        Assert.Equal("Roadmap", persisted.Name);
    }

    [Fact]
    public async Task DeleteBoardAsync_ShouldRemoveBoardColumnsAndCards()
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
        Assert.Null(DbContextForAssert.Boards.SingleOrDefault(x => x.Id == board.BoardId));
        Assert.Null(DbContextForAssert.Columns.SingleOrDefault(x => x.Id == columnId));
        Assert.Null(DbContextForAssert.Cards.SingleOrDefault(x => x.Id == cardId));
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
        var owner = new EntityUser
        {
            UserName = $"owner-{Guid.NewGuid():N}",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(owner);
        await DbContextForArrange.SaveChangesAsync();

        var board = new EntityBoard
        {
            Name = boardName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        board.Members.Add(new EntityBoardMember
        {
            UserId = owner.Id,
            Role = BoardMemberRole.Owner,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        DbContextForArrange.Boards.Add(board);
        await DbContextForArrange.SaveChangesAsync();
    }
}
