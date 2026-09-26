using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardChecklistCountsPersistenceTests : TestBaseDb
{
    private const string Checklist = "- [x] Done\n- [ ] Open";

    [Fact]
    public async Task Create_ShouldPersistAndPublishCounts()
    {
        var board = CreateBoard().AddColumn("Todo").Build();

        var result = await ResolveService<CardService>().CreateCardAsync(board.BoardId,
            new CreateCardRequest(board.GetColumn("Todo").Id, "Checklist", Checklist, []), ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, 1, 2);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(Checklist, stored.Description);
        Assert.Equal(1, stored.CompletedChecklistItemCount);
        Assert.Equal(2, stored.TotalChecklistItemCount);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        AssertCounts(Assert.Single(events.CardCreatedEvents).Card, 1, 2);
    }

    [Theory]
    [InlineData("- [x] Done\n- [x] Now done", 2, 2)]
    [InlineData("No tasks", 0, 0)]
    public async Task DescriptionChange_ShouldReplaceCounts(string description, int completed, int total)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Checklist", Checklist).Build();
        var card = board.GetCard("Checklist");
        card.CompletedChecklistItemCount = 1;
        card.TotalChecklistItemCount = 2;
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(board.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, description, [], card.CardTypeId), ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, completed, total);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(description, stored.Description);
        Assert.Equal(completed, stored.CompletedChecklistItemCount);
        Assert.Equal(total, stored.TotalChecklistItemCount);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        AssertCounts(Assert.Single(events.CardUpdatedEvents).Card, completed, total);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedDescription_ShouldRetainLegacyZeroCounts(bool changeTitle)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Legacy", Checklist).Build();
        var card = board.GetCard("Legacy");

        var result = await ResolveService<CardService>().UpdateCardAsync(board.BoardId, card.BoardCardId,
            new UpdateCardRequest(changeTitle ? "Renamed" : card.Title, Checklist, [], card.CardTypeId), ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, 0, 0);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(0, stored.TotalChecklistItemCount);
        Assert.Equal(Checklist, stored.Description);
    }

    [Fact]
    public async Task Read_ShouldNotPopulateLegacyCountsOrChangeTimestamps()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Legacy", Checklist).Build();
        var card = board.GetCard("Legacy");

        var result = await ResolveService<CardService>().GetCardAsync(board.BoardId, card.BoardCardId, ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, 0, 0);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(0, stored.TotalChecklistItemCount);
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Equal(card.UpdatedAtUtc, stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task RejectedUpdate_ShouldPreserveDescriptionAndCounts()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Checklist", Checklist).Build();
        var card = board.GetCard("Checklist");
        card.CompletedChecklistItemCount = 1;
        card.TotalChecklistItemCount = 2;
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(board.BoardId, card.BoardCardId,
            new UpdateCardRequest("", "No tasks", [], card.CardTypeId), ActorUserId);

        Assert.False(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(Checklist, stored.Description);
        Assert.Equal(1, stored.CompletedChecklistItemCount);
        Assert.Equal(2, stored.TotalChecklistItemCount);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).CardUpdatedEvents);
    }

    [Fact]
    public async Task Duplicate_ShouldCalculateFromDescriptionRatherThanCopyLegacyZero()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Legacy", Checklist).Build();
        var card = board.GetCard("Legacy");

        var result = await ResolveService<CardService>().DuplicateCardAsync(board.BoardId, card.BoardCardId,
            new CreateCardRequest(card.BoardColumnId, "Copy", Checklist, []), ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, 1, 2);
        Assert.Equal(0, (await DbContextForAssert.Cards.SingleAsync(x => x.Title == "Legacy")).TotalChecklistItemCount);
        Assert.Equal(2, (await DbContextForAssert.Cards.SingleAsync(x => x.Title == "Copy")).TotalChecklistItemCount);
    }

    [Fact]
    public async Task RestoreLegacySnapshot_ShouldRecalculateMissingCounts()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var column = board.GetColumn("Todo");
        var snapshot = $$"""
            {"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-26T12:00:00Z","payload":{
              "boardId":{{board.BoardId}},"originalCardId":80,"boardColumnId":{{column.Id}},
              "originalColumnName":"Todo","cardTypeId":1,"cardTypeName":"Story","cardTypeEmoji":null,
              "title":"Legacy archive","description":{{JsonSerializer.Serialize(Checklist)}},"sortKey":"A",
              "tags":[],"tagNames":[],"createdAtUtc":"2026-04-26T12:00:00Z","updatedAtUtc":"2026-04-26T12:00:00Z"}
            }
            """;
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 80, SnapshotJson = snapshot,
            SearchTitle = "Legacy archive", SearchTagsJson = "[]", SearchTextNormalised = "LEGACY ARCHIVE",
            ArchivedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 80, ActorUserId);

        Assert.True(result.Success);
        AssertCounts(result.Data!, 1, 2);
        Assert.Equal(2, (await DbContextForAssert.Cards.SingleAsync()).TotalChecklistItemCount);
    }

    private static void AssertCounts(CardDto card, int completed, int total)
    {
        Assert.Equal(completed, card.CompletedChecklistItemCount);
        Assert.Equal(total, card.TotalChecklistItemCount);
    }
}
