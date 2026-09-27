using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Jobs;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardResaveTests : TestBaseDb
{
    private const string Description = "- [x] Done\n- [ ] Open";

    protected override void ConfigureTestServices(IServiceCollection services) => services.AddLogging();

    [Fact]
    public async Task Resave_ShouldUseOrdinarySaveNormalisationAndPreserveRelationships()
    {
        var ordinary = CreateBoard("Ordinary").AddColumn("Todo").AddCard("  Card  ", Description).Build();
        var resaved = CreateBoard("Resaved").AddColumn("Todo").AddCard("  Card  ", Description).Build();
        var ordinaryCard = ordinary.GetCard("  Card  ");
        var resavedCard = resaved.GetCard("  Card  ");
        resavedCard.AssignedUserId = ActorUserId;
        resavedCard.Slick = new EntitySlick
        {
            BoardId = resaved.BoardId, Name = "Slick", NormalisedName = "SLICK",
            StyleName = "presets", StylePropertiesJson = "{\"presetIndex\":1}"
        };
        resavedCard.CardTags.Add(new EntityCardTag
        {
            Tag = new EntityTag
            {
                BoardId = resaved.BoardId, Name = "Tag", NormalisedName = "TAG",
                StyleName = "auto", StylePropertiesJson = "{}"
            }
        });
        await DbContextForArrange.SaveChangesAsync();
        var slickId = resavedCard.SlickId;
        var tagId = Assert.Single(resavedCard.CardTags).TagId;
        var normalSave = await ResolveService<CardService>().UpdateCardAsync(ordinary.BoardId, ordinaryCard.BoardCardId,
            new UpdateCardRequest(ordinaryCard.Title, ordinaryCard.Description, [], ordinaryCard.CardTypeId), ActorUserId);
        Assert.True(normalSave.Success);

        var result = await ResolveService<IBoardResaveService>().ResaveAsync(resaved.BoardId);

        Assert.Equal(1, result.CardsChanged);
        var stored = await DbContextForAssert.Cards.Include(x => x.CardTags).SingleAsync(x => x.Id == resavedCard.Id);
        Assert.Equal("Card", stored.Title);
        Assert.Equal(normalSave.Data!.Title, stored.Title);
        Assert.Equal(normalSave.Data.CompletedChecklistItemCount, stored.CompletedChecklistItemCount);
        Assert.Equal(normalSave.Data.TotalChecklistItemCount, stored.TotalChecklistItemCount);
        Assert.Equal(ActorUserId, stored.AssignedUserId);
        Assert.Equal(slickId, stored.SlickId);
        Assert.Equal(tagId, Assert.Single(stored.CardTags).TagId);
        Assert.Equal(resavedCard.CardTypeId, stored.CardTypeId);
        Assert.Equal(resavedCard.BoardCardId, stored.BoardCardId);
        Assert.Equal(resavedCard.SortKey, stored.SortKey);
        Assert.Equal(resavedCard.BoardColumnId, stored.BoardColumnId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resave_ShouldPreserveAssigneeWhoIsNoLongerAnActiveMember(bool removeMembership)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Assigned", Description)
            .AddCard("Unassigned", Description).Build();
        var assignedCard = board.GetCard("Assigned");
        assignedCard.AssignedUserId = ActorUserId;
        if (removeMembership)
        {
            var membership = await DbContextForArrange.BoardMembers.SingleAsync(x =>
                x.BoardId == board.BoardId && x.UserId == ActorUserId);
            DbContextForArrange.BoardMembers.Remove(membership);
        }
        else
        {
            var user = await DbContextForArrange.Users.SingleAsync(x => x.Id == ActorUserId);
            user.IsActive = false;
        }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId);

        Assert.Equal(new BoardResaveResult(board.BoardId, false, 2, 2), result);
        var storedCards = await DbContextForAssert.Cards.ToListAsync();
        Assert.All(storedCards, card =>
        {
            Assert.Equal(1, card.CompletedChecklistItemCount);
            Assert.Equal(2, card.TotalChecklistItemCount);
        });
        var storedAssignedCard = Assert.Single(storedCards, card => card.Id == assignedCard.Id);
        Assert.Equal(ActorUserId, storedAssignedCard.AssignedUserId);
        Assert.Equal(assignedCard.CardUpdatedUtc, storedAssignedCard.CardUpdatedUtc);
        Assert.Equal([board.BoardId], Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Resave_ShouldApplyOrdinaryValidationAndRollbackTheBoard()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Valid", Description).AddCard("", Description).Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId));

        Assert.Contains("Card title is required", error.Message);
        Assert.All(await DbContextForAssert.Cards.ToListAsync(), card => Assert.Equal(0, card.TotalChecklistItemCount));
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Resave_ShouldRepairLiveCountsAndPreserveContentTimestampsStylesAndArchives()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Legacy", Description)
            .AddColumn("Done").AddCard("Plain", "No checklist").Build();
        var card = board.GetCard("Todo", "Legacy");
        var plain = board.GetCard("Done", "Plain");
        plain.CompletedChecklistItemCount = 8;
        plain.TotalChecklistItemCount = 9;
        card.CardType.StylePropertiesJson = "{legacy-invalid";
        const string snapshot = "{\"schema\":\"archived-card\",\"version\":99,\"payload\":{}}";
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 99, SnapshotJson = snapshot,
            SearchTitle = "Archive", SearchTagsJson = "[]", ArchivedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId);

        Assert.Equal(new BoardResaveResult(board.BoardId, false, 2, 2), result);
        var stored = await DbContextForAssert.Cards.Include(x => x.CardType).SingleAsync(x => x.Id == card.Id);
        Assert.Equal(1, stored.CompletedChecklistItemCount);
        Assert.Equal(2, stored.TotalChecklistItemCount);
        Assert.Equal(Description, stored.Description);
        Assert.Equal(card.Title, stored.Title);
        Assert.Equal(card.SortKey, stored.SortKey);
        Assert.Equal(card.BoardColumnId, stored.BoardColumnId);
        Assert.Equal(card.CardCreatedUtc, stored.CardCreatedUtc);
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Equal(card.CreatedAtUtc, stored.CreatedAtUtc);
        Assert.True(stored.UpdatedAtUtc >= card.UpdatedAtUtc);
        Assert.Equal("{legacy-invalid", stored.CardType.StylePropertiesJson);
        var noChecklist = await DbContextForAssert.Cards.SingleAsync(x => x.Id == plain.Id);
        Assert.Equal(0, noChecklist.TotalChecklistItemCount);
        Assert.Equal(0, noChecklist.CompletedChecklistItemCount);
        Assert.Equal(snapshot, (await DbContextForAssert.ArchivedCards.SingleAsync()).SnapshotJson);
        Assert.Equal([board.BoardId], Events.ResyncRequestedBoardIds);
        Assert.Empty(Events.CardUpdatedEvents);
    }

    [Fact]
    public async Task RepeatedResave_ShouldNotChangeTimestampsOrPublishAnotherResync()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Checklist", Description).Build();
        await ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId);
        var before = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        Events.ResyncRequestedBoardIds.Clear();

        var result = await ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId);

        Assert.Equal(new BoardResaveResult(board.BoardId, false, 1, 0), result);
        Assert.Equal(before.UpdatedAtUtc, (await DbContextForAssert.Cards.AsNoTracking().SingleAsync()).UpdatedAtUtc);
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task EmptyBoard_ShouldCompleteWithoutChanges()
    {
        var board = CreateBoard().AddColumn("Todo").Build();

        var result = await ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId);

        Assert.Equal(new BoardResaveResult(board.BoardId, false, 0, 0), result);
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task DeletedBoardJob_ShouldCompleteAsSkipped()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var queued = await ResolveService<IScheduledJobService>().RunNowAsync(ResaveAllBoardsScheduledJobDefinition.ScheduleName);
        await DbContextForArrange.Boards.Where(x => x.Id == board.BoardId).ExecuteDeleteAsync();

        Assert.True(await ResolveService<IJobRunner>().RunNextDueAsync());

        var job = await DbContextForAssert.Jobs.SingleAsync(x => x.Id == queued.Data!.JobIds[0]);
        Assert.Equal(JobStatus.Completed, job.Status);
        using var result = JsonDocument.Parse(job.ResultJson);
        Assert.True(result.RootElement.GetProperty("skipped").GetBoolean());
        Assert.Equal(board.BoardId, result.RootElement.GetProperty("boardId").GetInt32());
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task FanOut_ShouldQueueOneJobPerBoardWithoutAnAutomaticRequest()
    {
        var first = CreateBoard("First").AddColumn("Todo").AddCard("One", Description).Build();
        var second = CreateBoard("Second").AddColumn("Todo").AddCard("Two", Description).Build();

        var queued = await ResolveService<IScheduledJobService>().RunNowAsync(
            ResaveAllBoardsScheduledJobDefinition.ScheduleName, ActorUserId);

        Assert.True(queued.Success);
        Assert.Equal(2, queued.Data!.EnqueuedCount);
        var jobs = await DbContextForAssert.Jobs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal([first.BoardId, second.BoardId], jobs.Select(x => JsonSerializer.Deserialize<ResaveBoardJobPayload>(x.PayloadJson)!.BoardId));
        Assert.All(jobs, job =>
        {
            Assert.Equal(ResaveBoardJobHandler.JobType, job.Type);
            Assert.Equal(JobStatus.Pending, job.Status);
            Assert.Equal(ActorUserId, job.UserId);
        });
        Assert.Empty(await DbContextForAssert.ScheduledJobSchedulerStates.ToListAsync());
    }

    [Fact]
    public async Task FailedBoard_ShouldRollbackWholeBoardAndAllowNextBoardToComplete()
    {
        var first = CreateBoard("Failing").AddColumn("Todo")
            .AddCard("First change", Description).AddCard("Rejected", Description).Build();
        var second = CreateBoard("Successful").AddColumn("Todo").AddCard("Good", Description).Build();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectResave BEFORE UPDATE OF "TotalChecklistItemCount" ON "Cards"
            WHEN NEW."Title" = 'Rejected'
            BEGIN SELECT RAISE(ABORT, 'Injected resave failure'); END;
            """);
        await ResolveService<IScheduledJobService>().RunNowAsync(ResaveAllBoardsScheduledJobDefinition.ScheduleName);
        var runner = ResolveService<IJobRunner>();

        Assert.True(await runner.RunNextDueAsync());
        Assert.True(await runner.RunNextDueAsync());

        var jobs = await DbContextForAssert.Jobs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(JobStatus.Failed, jobs[0].Status);
        Assert.Equal(JobStatus.Completed, jobs[1].Status);
        Assert.All(await DbContextForAssert.Cards.Where(x => x.BoardId == first.BoardId).ToListAsync(),
            card => Assert.Equal(0, card.TotalChecklistItemCount));
        Assert.Equal(2, (await DbContextForAssert.Cards.SingleAsync(x => x.BoardId == second.BoardId)).TotalChecklistItemCount);
        Assert.Equal([second.BoardId], Events.ResyncRequestedBoardIds);
        using var result = JsonDocument.Parse(jobs[1].ResultJson);
        Assert.Equal(1, result.RootElement.GetProperty("cardsExamined").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("cardsChanged").GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"BoardId\":-1}")]
    [InlineData("{broken")]
    public async Task InvalidPayload_ShouldFailWithoutChangingBoards(string payload)
    {
        var handler = ResolveService<IEnumerable<IJobHandler>>().Single(x => x.Type == ResaveBoardJobHandler.JobType);

        var result = await handler.HandleAsync(new JobContext(1, handler.Type, payload), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task CancelledResave_ShouldNotModifyBoard()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Checklist", Description).Build();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResolveService<IBoardResaveService>().ResaveAsync(board.BoardId, cancellation.Token));

        Assert.Equal(0, (await DbContextForAssert.Cards.SingleAsync()).TotalChecklistItemCount);
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
}
