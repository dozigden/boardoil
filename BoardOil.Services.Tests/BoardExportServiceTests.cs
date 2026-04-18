using System.IO.Compression;
using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardExportServiceTests : TestBaseDb
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ExportBoardAsync_ShouldReturnZipWithManifestAndBoardPayload()
    {
        var board = CreateBoard("Export Board")
            .AddColumn("Todo")
            .AddCard("Task A", "Description A")
            .Build();
        var boardEntity = DbContextForArrange.Boards.Single(x => x.Id == board.BoardId);
        boardEntity.Description = "Export board description";
        await DbContextForArrange.SaveChangesAsync();
        var card = board.GetCard("Todo", "Task A");
        var now = DateTime.UtcNow;

        var customCardType = new EntityCardType
        {
            BoardId = board.BoardId,
            Name = "Bug",
            Emoji = "B",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#FEEFC3","textColorMode":"auto"}""",
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.CardTypes.Add(customCardType);

        var urgentTag = new EntityTag
        {
            BoardId = board.BoardId,
            Name = "Urgent",
            NormalisedName = "URGENT",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#ED333B","textColorMode":"auto"}""",
            Emoji = "!",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Tags.Add(urgentTag);
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.CardTags.Add(new EntityCardTag
        {
            CardId = card.Id,
            TagId = urgentTag.Id
        });
        await DbContextForArrange.SaveChangesAsync();

        var service = ResolveService<IBoardExportService>();

        var result = await service.ExportBoardAsync(board.BoardId, ActorUserId, "0.2.0");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("application/zip", result.Data!.ContentType);
        Assert.Equal("Export-Board.boardoil.zip", result.Data.FileName);
        Assert.NotEmpty(result.Data.Content);

        using var stream = new MemoryStream(result.Data.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var manifestReader = new StreamReader(manifestEntry!.Open());
        var manifestJson = await manifestReader.ReadToEndAsync();
        var manifest = JsonSerializer.Deserialize<BoardPackageManifestDto>(manifestJson, JsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal("boardoil-board-package", manifest!.Format);
        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal("0.2.0", manifest.ExportedByVersion);
        Assert.Single(manifest.Entries);
        Assert.Equal("board", manifest.Entries[0].Kind);
        Assert.Equal("board.json", manifest.Entries[0].Path);

        var boardEntry = archive.GetEntry("board.json");
        Assert.NotNull(boardEntry);
        using var boardReader = new StreamReader(boardEntry!.Open());
        var boardJson = await boardReader.ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<BoardPackageBoardDto>(boardJson, JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Export Board", payload!.Name);
        Assert.Equal("Export board description", payload.Description);
        Assert.Contains(
            payload.CardTypes,
            x => x.Name == "Story"
                && x.IsSystem
                && x.StyleName == "solid"
                && !string.IsNullOrWhiteSpace(x.StylePropertiesJson));
        Assert.Contains(
            payload.CardTypes,
            x => x.Name == "Bug"
                && !x.IsSystem
                && x.Emoji == "B"
                && x.StyleName == "solid"
                && x.StylePropertiesJson == """{"backgroundColor":"#FEEFC3","textColorMode":"auto"}""");
        Assert.Contains(payload.Tags, x => x.Name == "Urgent" && x.StyleName == "solid" && x.Emoji == "!");
        Assert.Single(payload.Columns);
        Assert.Equal("Todo", payload.Columns[0].Title);
        Assert.Single(payload.Columns[0].Cards);
        Assert.Equal("Task A", payload.Columns[0].Cards[0].Title);
        Assert.Equal("Story", payload.Columns[0].Cards[0].CardTypeName);
        Assert.Equal(["Urgent"], payload.Columns[0].Cards[0].TagNames);
    }

    [Fact]
    public async Task ExportBoardAsync_WhenBoardDoesNotExist_ShouldReturnNotFound()
    {
        var service = ResolveService<IBoardExportService>();

        var result = await service.ExportBoardAsync(999999, ActorUserId, "0.2.0");

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task ExportBoardAsync_WhenActorHasNoBoardPermission_ShouldReturnForbidden()
    {
        var foreignBoardId = await CreateForeignBoardAsync("Private Board");
        var service = ResolveService<IBoardExportService>();

        var result = await service.ExportBoardAsync(foreignBoardId, ActorUserId, "0.2.0");

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("You do not have permission for this action.", result.Message);
    }

    private async Task<int> CreateForeignBoardAsync(string boardName)
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
        board.CardTypes.Add(CardTypeDefaults.CreateSystemForBoard(board, now));
        await DbContextForArrange.SaveChangesAsync();

        return board.Id;
    }
}
