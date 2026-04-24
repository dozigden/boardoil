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
    public async Task ExportBoardAsync_ShouldReturnZipWithManifestBoardPayloadAndArchivePayload()
    {
        var board = CreateBoard("Export Board")
            .AddColumn("Todo")
            .AddCard("Task A", "Description A")
            .AddCard("Task B", "Description B")
            .Build();
        var boardEntity = DbContextForArrange.Boards.Single(x => x.Id == board.BoardId);
        boardEntity.Description = "Export board description";
        await DbContextForArrange.SaveChangesAsync();
        var card = board.GetCard("Todo", "Task A");
        var unassignedCard = board.GetCard("Todo", "Task B");
        var now = DateTime.UtcNow;
        var actor = DbContextForArrange.Users.Single(x => x.Id == ActorUserId);

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
        var cardEntity = DbContextForArrange.Cards.Single(x => x.Id == card.Id);
        cardEntity.AssignedUserId = actor.Id;
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId,
            OriginalCardId = card.Id,
            ArchivedAtUtc = now,
            SnapshotJson = """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T00:00:00Z","payload":{"title":"Task A"}}""",
            SearchTitle = "Task A",
            SearchTagsJson = """["Urgent"]""",
            SearchTextNormalised = "TASK A URGENT"
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
        Assert.Contains(manifest.Entries, x => x.Kind == "board" && x.Path == "board.json");
        Assert.Contains(manifest.Entries, x => x.Kind == "archive" && x.Path == "archive.json");

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
        Assert.Equal(2, payload.Columns[0].Cards.Count);
        var exportedAssignedCard = payload.Columns[0].Cards.Single(x => x.Title == "Task A");
        Assert.Equal("Story", exportedAssignedCard.CardTypeName);
        Assert.Equal(["Urgent"], exportedAssignedCard.TagNames);
        Assert.Equal(actor.Email, exportedAssignedCard.AssignedUserEmail);
        var exportedUnassignedCard = payload.Columns[0].Cards.Single(x => x.Title == unassignedCard.Title);
        Assert.Equal("Story", exportedUnassignedCard.CardTypeName);
        Assert.Equal([], exportedUnassignedCard.TagNames);
        Assert.Null(exportedUnassignedCard.AssignedUserEmail);

        var archiveEntry = archive.GetEntry("archive.json");
        Assert.NotNull(archiveEntry);
        using var archiveReader = new StreamReader(archiveEntry!.Open());
        var archiveJson = await archiveReader.ReadToEndAsync();
        var archivePayload = JsonSerializer.Deserialize<BoardPackageArchiveDto>(archiveJson, JsonOptions);
        Assert.NotNull(archivePayload);
        Assert.Single(archivePayload!.Cards);
        Assert.Equal(card.Id, archivePayload.Cards[0].OriginalCardId);
        Assert.Equal("Task A", archivePayload.Cards[0].Title);
        Assert.Equal(["Urgent"], archivePayload.Cards[0].TagNames);
        Assert.Contains("\"schema\":\"archived-card\"", archivePayload.Cards[0].SnapshotJson, StringComparison.Ordinal);
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
        var ownerUserName = $"owner-{Guid.NewGuid():N}";
        var ownerEmail = $"{ownerUserName}@localhost";
        var owner = new EntityUser
        {
            UserName = ownerUserName,
            Email = ownerEmail,
            NormalisedEmail = ownerEmail.ToLowerInvariant(),
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
