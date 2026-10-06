using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.CardField;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardTypeChangePreviewTests : TestBaseDb
{
    [Fact]
    public void EquivalentFields_ShouldIgnoreCodesCasingOrderEmojiAndSettings()
    {
        CardFieldDefinition[] source =
        [
            new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", Nullable = true },
            new TextFieldDefinition { FieldCode = "Note", DisplayName = "Note", Nullable = true }
        ];
        CardFieldDefinition[] destination =
        [
            new TextFieldDefinition { FieldCode = "OtherNote", DisplayName = "NOTE", Nullable = true, MaxLength = 20, DefaultValue = "Default" },
            new IntegerFieldDefinition { FieldCode = "Effort", DisplayName = "estimate", Nullable = true, Emoji = "🎯", ShowOnCard = true, Min = 0 }
        ];
        var (card, target) = MakeCard(source, """{"Points":0,"Note":null}""", destination);

        var result = new UpdateCardPlanner().PreviewFields(card, target, null);

        Assert.True(result.IsValid);
        Assert.False(result.Value!.RequiresConfirmation);
        Assert.False(result.Value.HasLosses);
        Assert.Equal("estimate", result.Value.Rows[0].Destination!.DisplayName);
        Assert.Equal(0, result.Value.Rows[0].Destination!.Value.GetInt32());
    }

    [Fact]
    public void FieldlessTypes_ShouldNotRequireConfirmation()
    {
        var (card, target) = MakeCard([], "{}", []);

        var result = new UpdateCardPlanner().PreviewFields(card, target, null);

        Assert.False(result.Value!.RequiresConfirmation);
        Assert.Empty(result.Value.Rows);
    }

    [Fact]
    public void Rows_ShouldAlignConversionsReplacementsRemovalsAndAdditions()
    {
        CardFieldDefinition[] source =
        [
            new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", Nullable = true },
            new TextFieldDefinition { FieldCode = "Bad", DisplayName = "Quantity", Nullable = true },
            new DateFieldDefinition { FieldCode = "Due", DisplayName = "Due date", Nullable = true },
            new TextFieldDefinition { FieldCode = "Empty", DisplayName = "Empty", Nullable = true }
        ];
        CardFieldDefinition[] destination =
        [
            new IntegerFieldDefinition { FieldCode = "Quantity", DisplayName = "Quantity", DefaultValue = 2 },
            new TextFieldDefinition { FieldCode = "Effort", DisplayName = "Estimate", Nullable = true },
            new DateFieldDefinition { FieldCode = "New", DisplayName = "New date", DefaultValue = new DateOnly(2026, 10, 6) }
        ];
        var (card, target) = MakeCard(source, """{"Points":0,"Bad":"invalid","Due":"2026-01-01","Empty":null}""", destination);
        var edited = CardFieldJsonCodec.DeserialiseValues("""{"Points":7,"Bad":" edited ","Due":"2026-02-01","Empty":null}""");

        var result = new UpdateCardPlanner().PreviewFields(card, target, edited);

        Assert.True(result.IsValid);
        var preview = result.Value!;
        Assert.True(preview.RequiresConfirmation);
        Assert.True(preview.HasLosses);
        Assert.Equal(5, preview.Rows.Count);
        Assert.Equal(7, preview.Rows[0].Original!.Value.GetInt32());
        Assert.Equal("7", preview.Rows[0].Destination!.Value.GetString());
        Assert.False(preview.Rows[0].LosesValue);
        Assert.Equal("edited", preview.Rows[1].Original!.Value.GetString());
        Assert.Equal(2, preview.Rows[1].Destination!.Value.GetInt32());
        Assert.True(preview.Rows[1].LosesValue);
        Assert.Null(preview.Rows[2].Destination);
        Assert.True(preview.Rows[2].LosesValue);
        Assert.Null(preview.Rows[3].Destination);
        Assert.False(preview.Rows[3].LosesValue);
        Assert.Null(preview.Rows[4].Original);
        Assert.Equal("2026-10-06", preview.Rows[4].Destination!.Value.GetString());
    }

    [Theory]
    [InlineData("integer", "0", true)]
    [InlineData("integer", "null", false)]
    [InlineData("text", "null", false)]
    public void ChangedKindOrReplacement_ShouldRequireConfirmation(string targetKind, string value, bool hasLosses)
    {
        CardFieldDefinition original = new IntegerFieldDefinition { FieldCode = "Original", DisplayName = "Value", Nullable = true };
        CardFieldDefinition target = targetKind == "text"
            ? new TextFieldDefinition { FieldCode = "Target", DisplayName = "Value", Nullable = true }
            : new IntegerFieldDefinition { FieldCode = "Target", DisplayName = "Value", Min = 1, DefaultValue = 1 };
        var (card, destination) = MakeCard([original], $$"""{"Original":{{value}}}""", [target]);

        var result = new UpdateCardPlanner().PreviewFields(card, destination, null);

        Assert.True(result.Value!.RequiresConfirmation);
        Assert.Equal(hasLosses, result.Value.HasLosses);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Points\":\"invalid\"}")]
    [InlineData("{\"Points\":0,\"Unknown\":1}")]
    public void InvalidOriginalDraft_ShouldReturnValidationErrors(string json)
    {
        var (card, target) = MakeCard(
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", Nullable = true }],
            """{"Points":0}""", []);

        var result = new UpdateCardPlanner().PreviewFields(card, target, CardFieldJsonCodec.DeserialiseValues(json));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Preview_ShouldBeReadOnlyAndAgreeWithSaveUsingEditedValues()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", Nullable = true }]);
        card.FieldValuesJson = """{"Points":0}""";
        var target = new EntityCardType { BoardId = board.BoardId, Name = "Target" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        var fields = CardFieldJsonCodec.DeserialiseValues("""{"Points":7}""");
        var service = ResolveService<CardService>();

        var preview = await service.PreviewTypeChangeAsync(board.BoardId, card.BoardCardId,
            new(target.Id, fields), ActorUserId);
        var unchanged = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        var commentsBeforeSave = await DbContextForAssert.CardComments.CountAsync();
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        var updatesBeforeSave = events.CardUpdatedEvents.Count;
        var saved = await service.UpdateCardAsync(board.BoardId, card.BoardCardId,
            new(card.Title, card.Description, [], target.Id, Fields: fields), ActorUserId);

        Assert.True(preview.Success);
        Assert.True(preview.Data!.HasLosses);
        Assert.Equal(7, preview.Data.Rows[0].Original!.Value.GetInt32());
        Assert.Equal(card.CardTypeId, unchanged.CardTypeId);
        Assert.Equal(card.FieldValuesJson, unchanged.FieldValuesJson);
        Assert.Equal(0, commentsBeforeSave);
        Assert.Equal(0, updatesBeforeSave);
        Assert.True(saved.Success);
        Assert.Empty(saved.Data!.Fields);
        Assert.Contains("Estimate: 7", (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Theory]
    [InlineData(true, 403)]
    [InlineData(false, 400)]
    public async Task Preview_ShouldEnforceBoardAccessAndTargetBoard(bool unauthorised, int expectedStatus)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var other = CreateBoard("Other").AddColumn("Todo").Build();
        var target = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == other.BoardId);

        var result = await ResolveService<CardService>().PreviewTypeChangeAsync(board.BoardId,
            board.GetCard("Card").BoardCardId, new(target.Id), unauthorised ? int.MaxValue : ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.StatusCode);
    }

    private static (EntityBoardCard Card, EntityCardType Target) MakeCard(
        CardFieldDefinition[] source, string values, CardFieldDefinition[] destination)
    {
        var sourceType = new EntityCardType { Id = 1, Name = "Source", FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(source) };
        return (new EntityBoardCard { CardTypeId = 1, CardType = sourceType, FieldValuesJson = values },
            new EntityCardType { Id = 2, Name = "Destination", FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(destination) });
    }
}
