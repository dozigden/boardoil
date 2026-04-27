using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;
using ArchivedCardEntity = BoardOil.Persistence.Abstractions.Entities.EntityArchivedCard;

namespace BoardOil.Services.Tests;

public sealed class CardUnarchiveServiceV1Tests : TestBaseDb
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1Exists_ShouldRestoreLiveCardAndRemoveArchive()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12345,
            boardColumnId: todoColumnId,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(boardId),
            title: "Archive me",
            description: "Desc");
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.NotEqual(archivedCard.OriginalCardId, unarchiveResult.Data!.Id);
        Assert.Equal("Archive me", unarchiveResult.Data.Title);
        Assert.Equal("Desc", unarchiveResult.Data.Description);
        Assert.Equal(todoColumnId, unarchiveResult.Data.BoardColumnId);
        Assert.Empty(await DbContextForAssert.Set<ArchivedCardEntity>().ToListAsync());
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardMissing_ShouldReturnNotFound()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCardId: 999_999, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenOriginalColumnMissingInV1Snapshot_ShouldFallbackToFirstColumn()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12346,
            boardColumnId: 999999,
            cardTypeId: systemCardTypeId,
            title: "Archive me",
            description: "Desc");
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(todoColumnId, unarchiveResult.Data!.BoardColumnId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenV1SnapshotCardTypeMissing_ShouldFallbackToSystemCardType()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var now = DateTime.UtcNow;
        var customCardType = DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Bug",
            Emoji = "🐛",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();

        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12347,
            boardColumnId: board.GetColumn("Todo").Id,
            cardTypeId: customCardType.Id,
            title: "Archive me",
            description: "Desc");

        DbContextForArrange.CardTypes.Remove(customCardType);
        await DbContextForArrange.SaveChangesAsync();

        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(systemCardTypeId, unarchiveResult.Data!.CardTypeId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenV1SnapshotInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = board.BoardId,
            OriginalCardId = 123,
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = "not-json",
            SearchTitle = "Broken snapshot",
            SearchTagsJson = "[]",
            SearchTextNormalised = "BROKEN SNAPSHOT"
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    private async Task<ArchivedCardEntity> SeedArchivedCardV1Async(
        int boardId,
        int originalCardId,
        int boardColumnId,
        int cardTypeId,
        string title,
        string description)
    {
        var archivedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var snapshotJson = CreateSnapshotJsonV1(
            boardId,
            originalCardId,
            boardColumnId,
            cardTypeId,
            title,
            description,
            archivedAtUtc);
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = boardId,
            OriginalCardId = originalCardId,
            ArchivedAtUtc = archivedAtUtc,
            SnapshotJson = snapshotJson,
            SearchTitle = title,
            SearchTagsJson = "[]",
            SearchTextNormalised = title.ToUpperInvariant()
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        return archivedCard;
    }

    private static string CreateSnapshotJsonV1(
        int boardId,
        int originalCardId,
        int boardColumnId,
        int cardTypeId,
        string title,
        string description,
        DateTime capturedAtUtc)
    {
        var payload = new ArchivedCardSnapshotV1Payload(
            boardId,
            originalCardId,
            boardColumnId,
            "Todo",
            cardTypeId,
            "Story",
            null,
            title,
            description,
            "A",
            [],
            [],
            capturedAtUtc,
            capturedAtUtc,
            null);
        var envelope = new ArchivedCardSnapshotEnvelopeV1(
            ArchivedCardSnapshotSerialiser.SchemaName,
            1,
            capturedAtUtc,
            payload);
        return JsonSerializer.Serialize(envelope, SnapshotJsonOptions);
    }

    private Task<int> GetSystemCardTypeIdForBoardAsync(int boardId) =>
        DbContextForArrange.CardTypes
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
}

public sealed class CardUnarchiveServiceV1AuthorisationTests : TestBaseDb
{
    private readonly CapturingBoardAuthorisationService _boardAuthorisationService = new();

    [Fact]
    public async Task UnarchiveCardAsync_WhenPermissionDenied_ShouldCheckCardCreatePermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = board.BoardId,
            OriginalCardId = 123,
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = CreateAuthorisationSnapshotJsonV1(),
            SearchTitle = "Archive me",
            SearchTagsJson = "[]",
            SearchTextNormalised = "ARCHIVE ME"
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.CardCreate, _boardAuthorisationService.LastPermission);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(_boardAuthorisationService);
        services.AddScoped<IBoardAuthorisationService>(provider =>
            provider.GetRequiredService<CapturingBoardAuthorisationService>());
    }

    private static string CreateAuthorisationSnapshotJsonV1() =>
        """
        {"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-26T12:00:00Z","payload":{"boardId":1,"originalCardId":123,"boardColumnId":1,"originalColumnName":"Todo","cardTypeId":1,"cardTypeName":"Story","cardTypeEmoji":null,"title":"Archive me","description":"Desc","sortKey":"A","tags":[],"tagNames":[],"createdAtUtc":"2026-04-26T12:00:00Z","updatedAtUtc":"2026-04-26T12:00:00Z","assignedUserId":null}}
        """;

    private sealed class CapturingBoardAuthorisationService : IBoardAuthorisationService
    {
        public BoardPermission? LastPermission { get; private set; }

        public Task<bool> HasPermissionAsync(int boardId, int actorUserId, BoardPermission permission)
        {
            LastPermission = permission;
            return Task.FromResult(false);
        }
    }
}
