using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.CardField;
using BoardOil.Services.CardType;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldConversionIntegrationTests : TestBaseDb
{
    [Fact]
    public async Task RemoveFields_ShouldRecordMaximumSizedLiteralValuesOncePerPopulatedCard()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("One").AddCard("Two").AddCard("Empty").Build();
        var type = board.GetCard("One").CardType;
        type.Name = new string('`', 40);
        var renamedType = new string('~', 38) + "\nX";
        var definitions = Enumerable.Range(0, 10).Select(index => new TextFieldDefinition
        {
            FieldCode = $"Code{index}", DisplayName = index + new string('`', 39), Nullable = true
        }).ToArray();
        type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(definitions);
        var value = new string('`', 250);
        foreach (var name in new[] { "One", "Two", "Empty" })
        {
            board.GetCard(name).FieldValuesJson = CardFieldJsonCodec.SerialiseValues(definitions.ToDictionary(
                field => field.FieldCode, _ => JsonSerializer.SerializeToElement(name == "Empty" ? null : value)));
        }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(board.BoardId, type.Id,
            new UpdateCardTypeRequest(renamedType, Fields: []), ActorUserId);

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
                Assert.Contains($"    {definition.DisplayName}: {value}", comment.Text);
            }
        });
        Assert.All(await DbContextForAssert.Cards.ToListAsync(), card =>
        {
            Assert.Equal("{}", card.FieldValuesJson);
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
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
        [
            new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 0 },
            new TextFieldDefinition { FieldCode = "Ref", DisplayName = "Reference", Nullable = true }
        ]);
        card.FieldValuesJson = """{"Points":0,"Ref":"Lost value"}""";
        var target = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == destination.BoardId);
        target.Name = policy == CardTransferPolicies.DestinationDefaults ? "Target type" : "Source type";
        target.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [new TextFieldDefinition { FieldCode = "Effort", DisplayName = "estimate", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().TransferCardAsync(source.BoardId, card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, policy), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal("0", Assert.Single(result.Data!.Card.Fields).Value.GetString());
        Assert.True(result.Data.Card.Fields.ContainsKey("Effort"));
        Assert.Equal("Keep description", result.Data.Card.Description);
        Assert.Equal(target.FieldDefinitionsJson,
            (await DbContextForAssert.CardTypes.SingleAsync(type => type.Id == target.Id)).FieldDefinitionsJson);
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
    public async Task Transfer_CopyMissingType_ShouldCopyDefinitionsAndPreserveValuesWithoutLossComment()
    {
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move").Build();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var card = source.GetCard("Move");
        card.CardType.Name = "Custom";
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 0 }]);
        card.FieldValuesJson = """{"Points":7}""";
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().TransferCardAsync(source.BoardId, card.BoardCardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, CardTransferPolicies.CopyMissing), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(7, result.Data!.Card.Fields["Points"].GetInt32());
        var copiedType = await DbContextForAssert.CardTypes.SingleAsync(type => type.Id == result.Data.Card.CardTypeId);
        Assert.Equal(card.CardType.FieldDefinitionsJson, copiedType.FieldDefinitionsJson);
        Assert.False(copiedType.IsSystem);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Fact]
    public async Task Transfer_WhenCommentWriteFails_ShouldRollbackMoveValuesAndNewDefinitions()
    {
        var source = CreateBoard("Source").AddColumn("Todo").AddCard("Move").Build();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var card = source.GetCard("Move");
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 0 }]);
        card.FieldValuesJson = """{"Points":7}""";
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
        Assert.Equal(card.FieldValuesJson, stored.FieldValuesJson);
        Assert.Empty(await DbContextForAssert.Slicks.Where(slick => slick.BoardId == destination.BoardId).ToListAsync());
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Empty(Events.CommentCreatedEvents);
        Assert.Empty(Events.CardDeletedEvents);
        Assert.Empty(Events.CardCreatedEvents);
    }

    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
}
