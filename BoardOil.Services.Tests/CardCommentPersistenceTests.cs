using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardCommentPersistenceTests : TestBaseDb
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveComment_ShouldRoundTripOptionalEditTimestamp(bool hasEditTimestamp)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "").Build();
        var card = board.GetCard("Card");
        var postedAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime? editedAtUtc = null;
        if (hasEditTimestamp)
        {
            editedAtUtc = postedAtUtc.AddHours(1);
        }
        var comment = new EntityCardComment
        {
            CardId = card.Id,
            AuthorUserId = ActorUserId,
            Text = "Persisted comment",
            PostedAtUtc = postedAtUtc,
            CreatedAtUtc = postedAtUtc,
            EditedAtUtc = editedAtUtc
        };
        DbContextForArrange.CardComments.Add(comment);

        await DbContextForArrange.SaveChangesAsync();

        var stored = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(comment.Id, stored.Id);
        Assert.Equal(card.Id, stored.CardId);
        Assert.Equal(ActorUserId, stored.AuthorUserId);
        Assert.Equal(comment.Text, stored.Text);
        Assert.Equal(postedAtUtc, stored.PostedAtUtc);
        Assert.Equal(postedAtUtc, stored.CreatedAtUtc);
        Assert.Equal(editedAtUtc, stored.EditedAtUtc);
        if (stored.EditedAtUtc.HasValue)
        {
            Assert.Equal(DateTimeKind.Utc, stored.EditedAtUtc.Value.Kind);
        }
    }
}
