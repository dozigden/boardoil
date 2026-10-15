using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.CustomField;
using BoardOil.Services.CardType;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldConversionIntegrationTests : TestBaseDb
{
    [Fact]
    public async Task DeleteType_WhenCommentWriteFails_ShouldRollbackReassignmentValuesAndAssignments()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("One").AddCard("Two").Build();
        var type = board.GetCard("One").CardType;
        type.IsSystem = false;
        DbContextForArrange.CardTypes.Add(new EntityCardType { BoardId = board.BoardId, Name = "Replacement", IsSystem = true });
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }]);
        board.GetCard("One").CustomFieldValuesJson = """{"Ref":"First value"}""";
        board.GetCard("Two").CustomFieldValuesJson = """{"Ref":"Second value"}""";
        await DbContextForArrange.SaveChangesAsync();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectLoss BEFORE INSERT ON "CardComments"
            BEGIN SELECT RAISE(ABORT, 'Injected comment failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => ResolveService<CardTypeService>()
            .DeleteCardTypeAsync(board.BoardId, type.Id, ActorUserId));

        Assert.True(await DbContextForAssert.CardTypes.AnyAsync(item => item.Id == type.Id));
        Assert.Equal(type.Id, (await DbContextForAssert.CustomFieldCardTypes.SingleAsync()).CardTypeId);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.Title).ToListAsync();
        Assert.All(cards, card => Assert.Equal(type.Id, card.CardTypeId));
        Assert.Equal(board.GetCard("One").CustomFieldValuesJson, cards[0].CustomFieldValuesJson);
        Assert.Equal(board.GetCard("Two").CustomFieldValuesJson, cards[1].CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task DeleteType_WithInvalidCardValues_ShouldReturnCardScopedErrorsWithoutReassigningCards()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Valid").AddCard("Invalid").Build();
        var type = board.GetCard("Valid").CardType;
        type.IsSystem = false;
        DbContextForArrange.CardTypes.Add(new EntityCardType { BoardId = board.BoardId, Name = "Replacement", IsSystem = true });
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }]);
        board.GetCard("Valid").CustomFieldValuesJson = """{"Ref":"Saved"}""";
        board.GetCard("Invalid").CustomFieldValuesJson = "{}";
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardTypeService>().DeleteCardTypeAsync(board.BoardId, type.Id, ActorUserId);

        Assert.False(result.Success);
        Assert.Contains($"cards[{board.GetCard("Invalid").BoardCardId}].customFieldValues.Ref", result.ValidationErrors!.Keys);
        Assert.True(await DbContextForAssert.CardTypes.AnyAsync(item => item.Id == type.Id));
        Assert.All(await DbContextForAssert.Cards.ToListAsync(), card => Assert.Equal(type.Id, card.CardTypeId));
        Assert.Equal("""{"Ref":"Saved"}""", (await DbContextForAssert.Cards.SingleAsync(card => card.Title == "Valid")).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task RemoveFields_ShouldRecordMaximumSizedLiteralValuesOncePerPopulatedCard()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("One").AddCard("Two").AddCard("Empty").Build();
        var type = board.GetCard("One").CardType;
        type.Name = new string('`', 40);
        type.IsSystem = false;
        var replacement = new EntityCardType { BoardId = board.BoardId, Name = new string('~', 38) + "\nX", IsSystem = true };
        DbContextForArrange.CardTypes.Add(replacement);
        var definitions = Enumerable.Range(0, 10).Select(index => new TextCustomFieldDefinition
        {
            Code = $"Code{index}", Name = index + new string('`', 39), Nullable = true
        }).ToArray();
        SetFields(type, definitions);
        var value = new string('`', 250);
        foreach (var name in new[] { "One", "Two", "Empty" })
        {
            board.GetCard(name).CustomFieldValuesJson = CustomFieldJsonCodec.SerialiseValues(definitions.ToDictionary(
                field => field.Code, _ => JsonSerializer.SerializeToElement(name == "Empty" ? null : value)));
        }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardTypeService>().DeleteCardTypeAsync(board.BoardId, type.Id, ActorUserId);

        Assert.True(result.Success, result.Message);
        var comments = await DbContextForAssert.CardComments.Include(comment => comment.Card).ToListAsync();
        Assert.Equal(2, comments.Count);
        Assert.Equal(new[] { "One", "Two" }, comments.Select(comment => comment.Card.Title).Order());
        Assert.All(comments, comment =>
        {
            Assert.True(comment.IsSystem);
            Assert.Null(comment.AuthorUserId);
            Assert.InRange(comment.Text.Length, 1, 4_000);
            Assert.Contains($"    Card type: {type.Name} → {new string('~', 38)}\n    X\n", comment.Text);
            Assert.DoesNotContain("Code", comment.Text);
            foreach (var definition in definitions)
            {
                Assert.Contains($"    {definition.Name}: {value}", comment.Text);
            }
        });
        Assert.All(await DbContextForAssert.Cards.ToListAsync(), card =>
        {
            Assert.Equal("{}", card.CustomFieldValuesJson);
            Assert.Equal(string.Empty, card.Description);
        });
    }

    [Theory]
    [InlineData(CardTransferPolicies.DestinationDefaults)]
    [InlineData(CardTransferPolicies.KeepMatching)]
    [InlineData(CardTransferPolicies.CopyMissing)]
    public async Task Transfer_ShouldUseExistingDestinationFieldsAndRecordLosses(string policy)
    {
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move", "Keep description").Build();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var card = source.GetCard("Move");
        card.CardType.Name = "Source type";
        SetFields(card.CardType,
        [
            new IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", DefaultValue = 0 },
            new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }
        ]);
        card.CustomFieldValuesJson = """{"Points":0,"Ref":"Lost value"}""";
        var target = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == destination.BoardId);
        target.Name = policy == CardTransferPolicies.DestinationDefaults ? "Target type" : "Source type";
        SetFields(target,
            [new TextCustomFieldDefinition { Code = "Effort", Name = "estimate", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().TransferCardAsync(source.BoardId, card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, policy), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal("0", Assert.Single(result.Data!.Card.CustomFieldValues).Value.GetString());
        Assert.True(result.Data.Card.CustomFieldValues.ContainsKey("Effort"));
        Assert.Equal("Keep description", result.Data.Card.Description);
        Assert.Equal(target.Board.CustomFields.ToCustomFieldDtos().Select(field => field.Definition),
            (await DbContextForAssert.Boards.Include(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync(board => board.Id == target.BoardId)).CustomFields.ToCustomFieldDtos().Select(field => field.Definition));
        var comment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(card.Id, comment.CardId);
        Assert.Equal($"Fields removed during board transfer:\n\n    Card type: {card.CardType.Name} → {target.Name}\n\n    Reference: Lost value", comment.Text);
        Assert.True(comment.IsSystem);
        var notification = Assert.Single(Events.CommentCreatedEvents);
        Assert.Equal(destination.BoardId, notification.BoardId);
        Assert.Equal(result.Data.Card.Id, notification.Comment.CardId);
        Assert.True(notification.Comment.IsSystem);
    }

    [Fact]
    public async Task Transfer_CopyMissingType_ShouldLeaveDestinationCatalogueUnchangedAndRecordLoss()
    {
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move").Build();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var card = source.GetCard("Move");
        card.CardType.Name = "Custom";
        SetFields(card.CardType,
            [new IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", DefaultValue = 0 }]);
        card.CustomFieldValuesJson = """{"Points":7}""";
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().TransferCardAsync(source.BoardId, card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, CardTransferPolicies.CopyMissing), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Data!.Card.CustomFieldValues);
        var copiedType = await DbContextForAssert.CardTypes.SingleAsync(type => type.Id == result.Data.Card.CardTypeId);
        Assert.Empty(await DbContextForAssert.CustomFields.Where(field => field.BoardId == destination.BoardId).ToListAsync());
        Assert.False(copiedType.IsSystem);
        Assert.Contains("Estimate: 7", (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Fact]
    public async Task Transfer_WhenCommentWriteFails_ShouldRollbackMoveValuesAndNewDefinitions()
    {
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move").Build();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var card = source.GetCard("Move");
        SetFields(card.CardType,
            [new IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", DefaultValue = 0 }]);
        card.CustomFieldValuesJson = """{"Points":7}""";
        card.Slick = new EntitySlick
        {
            BoardId = source.BoardId, Name = "Copy", NormalisedName = "COPY", StyleName = "auto", StylePropertiesJson = "{}"
        };
        await DbContextForArrange.SaveChangesAsync();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectLossComment BEFORE INSERT ON "CardComments"
            WHEN NEW."IsSystem" = 1
            BEGIN SELECT RAISE(ABORT, 'Injected comment failure'); END;
            """);

        var result = await ResolveService<CardService>().TransferCardAsync(source.BoardId, card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, CardTransferPolicies.CopyMissing), ActorUserId);

        Assert.False(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(source.BoardId, stored.BoardId);
        Assert.Equal(card.BoardCardId, stored.BoardCardId);
        Assert.Equal(card.CustomFieldValuesJson, stored.CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.Slicks.Where(slick => slick.BoardId == destination.BoardId).ToListAsync());
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Empty(Events.CommentCreatedEvents);
        Assert.Empty(Events.CardDeletedEvents);
        Assert.Empty(Events.CardCreatedEvents);
    }

    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
}
