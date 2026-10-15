using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.CustomField;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldPersistenceTests : TestBaseDb
{
    private static readonly CustomFieldDefinition[] Definitions =
    [
        new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true, DefaultValue = "Default" },
        new IntegerCustomFieldDefinition { Code = "Points", Name = "Points", DefaultValue = 0, Min = 0, Max = 10 },
        new DateCustomFieldDefinition { Code = "Due", Name = "Due date", Nullable = true }
    ];

    [Fact]
    public async Task Create_ShouldPersistAndPublishDefaultsWithoutChangingCreationContract()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, Definitions);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().CreateCardAsync(board.BoardId,
            new CreateCardRequest(null, "Created", null, null), ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":0,\"Due\":null}", stored.CustomFieldValuesJson);
        Assert.Equal("Default", result.Data!.CustomFieldValues["Ref"].GetString());
        Assert.Equal(0, result.Data.CustomFieldValues["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Data.CustomFieldValues["Due"].ValueKind);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(stored.CustomFieldValuesJson, CustomFieldJsonCodec.SerialiseValues(Assert.Single(events.CardCreatedEvents).Card.CustomFieldValues));
    }

    [Fact]
    public async Task Create_WithInitialFields_ShouldNormaliseValuesAndDefaultOnlyMissingFields()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, Definitions);
        await DbContextForArrange.SaveChangesAsync();
        var fields = CustomFieldJsonCodec.DeserialiseValues("{\"ref\":null,\"due\":\"2024-02-29\"}");

        var result = await ResolveService<CardService>().CreateCardAsync(board.BoardId,
            new CreateCardRequest(null, "Created", null, null), ActorUserId, fields);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Ref\":null,\"Points\":0,\"Due\":\"2024-02-29\"}", stored.CustomFieldValuesJson);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(stored.CustomFieldValuesJson, CustomFieldJsonCodec.SerialiseValues(Assert.Single(events.CardCreatedEvents).Card.CustomFieldValues));
    }

    [Theory]
    [InlineData("{\"Points\":11}")]
    [InlineData("{\"Points\":null}")]
    [InlineData("{\"Unknown\":1}")]
    public async Task Create_WithInvalidInitialFields_ShouldNotCreateOrPublishCard(string json)
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, Definitions);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().CreateCardAsync(board.BoardId,
            new CreateCardRequest(null, "Invalid", null, null), ActorUserId, CustomFieldJsonCodec.DeserialiseValues(json));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await DbContextForAssert.Cards.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).CardCreatedEvents);
    }

    [Fact]
    public async Task Update_WhenFieldsOmitted_ShouldPreserveStoredValues()
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest("Renamed", card.Description, [], card.CardTypeId);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CustomFieldValuesJson, stored.CustomFieldValuesJson);
        Assert.Equal("Renamed", stored.Title);
        Assert.Equal(5, result.Data!.CustomFieldValues["Points"].GetInt32());
    }

    [Fact]
    public async Task Update_WhenOnlyFieldsChange_ShouldPersistNormalisedValuesAndPublishNewTimestamp()
    {
        var card = await ArrangeCardAsync();
        var values = CustomFieldJsonCodec.DeserialiseValues("{\"ref\":\"  Changed  \",\"points\":7,\"due\":\"2024-02-29\"}");
        var request = new UpdateCardRequest(card.Title, card.Description, [], card.CardTypeId, CustomFieldValues: values);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Ref\":\"Changed\",\"Points\":7,\"Due\":\"2024-02-29\"}", stored.CustomFieldValuesJson);
        Assert.True(stored.CardUpdatedUtc > card.CardUpdatedUtc);
        Assert.Equal(card.CardCreatedUtc, stored.CardCreatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(7, Assert.Single(events.CardUpdatedEvents).Card.CustomFieldValues["Points"].GetInt32());
        Assert.Equal("Description", stored.Description);
    }

    [Fact]
    public async Task Update_WhenValuesUnchanged_ShouldPreserveLogicalTimestamp()
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest(card.Title, card.Description, [], card.CardTypeId,
            CustomFieldValues: CustomFieldJsonCodec.DeserialiseValues(card.CustomFieldValuesJson));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(card.CardUpdatedUtc, result.Data!.CardUpdatedUtc);
    }

    [Theory]
    [InlineData("{\"Ref\":null,\"Points\":11,\"Due\":null}", "customFieldValues.Points")]
    [InlineData("{\"Ref\":null,\"Points\":0}", "customFieldValues.Due")]
    [InlineData("{\"Ref\":null,\"Points\":0,\"Due\":null,\"Unknown\":null}", "customFieldValues.Unknown")]
    public async Task Update_WhenFieldsInvalid_ShouldLeaveCardAndRelatedDataUnchanged(string json, string property)
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest("Do not save", "Changed description", ["New tag"], card.CardTypeId,
            SlickName: "New slick", CustomFieldValues: CustomFieldJsonCodec.DeserialiseValues(json));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.False(result.Success);
        Assert.Contains(property, result.ValidationErrors!.Keys);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.Title, stored.Title);
        Assert.Equal(card.Description, stored.Description);
        Assert.Equal(card.CustomFieldValuesJson, stored.CustomFieldValuesJson);
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
        Assert.Empty(await DbContextForAssert.Slicks.ToListAsync());
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.CardUpdatedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypeChange_ShouldApplyOriginalFieldEditsBeforeConversionAndRecordEditedLosses(bool supplyValues)
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        {
            BoardId = card.BoardId, Name = "Target", StyleName = "auto", StylePropertiesJson = "{}",
        };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        SetFields(target,
                [Definitions[1]]);
        await DbContextForArrange.SaveChangesAsync();
        IReadOnlyDictionary<string, JsonElement>? fields = null;
        if (supplyValues)
        {
            fields = CustomFieldJsonCodec.DeserialiseValues("{\"Ref\":\"Edited reference\",\"Points\":7,\"Due\":null}");
        }
        var request = new UpdateCardRequest(card.Title, "Edited description", [], target.Id, CustomFieldValues: fields);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(target.Id, stored.CardTypeId);
        Assert.Equal(supplyValues ? 7 : 5, result.Data!.CustomFieldValues["Points"].GetInt32());
        Assert.Single(result.Data.CustomFieldValues);
        Assert.Equal("Edited description", stored.Description);
        var comment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal($"Fields removed during card type change:\n\n    Card type: {card.CardType.Name} → Target\n\n    Reference: "
            + (supplyValues ? "Edited reference" : "Existing"), comment.Text);
        Assert.True(comment.IsSystem);
        Assert.Null(comment.AuthorUserId);
    }

    [Theory]
    [InlineData("{\"Ref\":null,\"Points\":11,\"Due\":null}")]
    [InlineData("{}")]
    public async Task TypeChange_WhenSuppliedFieldsInvalid_ShouldNotSaveConversionOrHistory(string json)
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        { BoardId = card.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        var request = new UpdateCardRequest(card.Title, card.Description, [], target.Id,
            CustomFieldValues: CustomFieldJsonCodec.DeserialiseValues(json));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.False(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CardTypeId, stored.CardTypeId);
        Assert.Equal(card.Description, stored.Description);
        Assert.Equal(card.CustomFieldValuesJson, stored.CustomFieldValuesJson);
    }

    [Fact]
    public async Task TypeChange_WithoutLosses_ShouldPreserveDescription()
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        {
            BoardId = card.BoardId, Name = "Same fields", StyleName = "auto", StylePropertiesJson = "{}",
        };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        SetFields(target, card.CardType.Board.CustomFields.GetApplicableCustomFieldDefinitions(card.CardType.Id));
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, card.Description, [], target.Id), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(card.Description, result.Data!.Description);
        Assert.Equal(card.CustomFieldValuesJson, CustomFieldJsonCodec.SerialiseValues(result.Data.CustomFieldValues));
    }

    [Fact]
    public async Task TypeChange_WithFullDescription_ShouldKeepDescriptionAndRecordComment()
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        { BoardId = card.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, new string('a', 20_000), [], target.Id), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new string('a', 20_000), result.Data!.Description);
        Assert.Equal(target.Id, result.Data.CardTypeId);
        Assert.True((await DbContextForAssert.CardComments.SingleAsync()).IsSystem);
    }

    [Fact]
    public async Task TypeChange_WhenLostTextLooksLikeMarkdown_ShouldRecordLiteralTextAndAllowSubsequentEdits()
    {
        var card = await ArrangeCardAsync();
        const string text = "![image](data:image/png;base64,AAAA)";
        card.CustomFieldValuesJson = CustomFieldJsonCodec.SerialiseValues(new Dictionary<string, JsonElement>
        {
            ["Ref"] = JsonSerializer.SerializeToElement(text),
            ["Points"] = JsonSerializer.SerializeToElement(5),
            ["Due"] = JsonSerializer.SerializeToElement<string?>(null)
        });
        var target = new EntityCardType { BoardId = card.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        var converted = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, card.Description, [], target.Id), ActorUserId);
        Assert.True(converted.Success);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest("Renamed", converted.Data!.Description, [], target.Id), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(card.Description, result.Data!.Description);
        Assert.Contains("    Reference: " + text, (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_ShouldPreserveSourceFieldsOrApplyCompleteDraftWithoutChangingSource(bool supplyFields)
    {
        var source = await ArrangeCardAsync();
        var fields = supplyFields ? CustomFieldJsonCodec.DeserialiseValues("""{"Ref":null,"Points":0,"Due":"2024-02-29"}""") : null;

        var result = await ResolveService<CardService>().DuplicateCardAsync(source.BoardId, source.BoardCardId,
            new CreateCardRequest(null, "Duplicate", source.Description, [], source.CardTypeId), ActorUserId, fields: fields);

        Assert.True(result.Success);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.Equal(2, cards.Count);
        Assert.Equal(source.CustomFieldValuesJson, cards[0].CustomFieldValuesJson);
        Assert.Equal(source.CardUpdatedUtc, cards[0].CardUpdatedUtc);
        var expected = supplyFields ? """{"Ref":null,"Points":0,"Due":"2024-02-29"}""" : source.CustomFieldValuesJson;
        Assert.Equal(expected, cards[1].CustomFieldValuesJson);
        Assert.Equal(source.Description, cards[1].Description);
        Assert.Equal(expected, CustomFieldJsonCodec.SerialiseValues(result.Data!.CustomFieldValues));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_WithDifferentType_ShouldConvertOriginalValuesAndRecordLossOnlyOnDuplicate(bool supplyFields)
    {
        var source = await ArrangeCardAsync();
        var target = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Target", StyleName = "auto", StylePropertiesJson = "{}",
        };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        SetFields(target,
                [Definitions[1]]);
        await DbContextForArrange.SaveChangesAsync();
        var fields = supplyFields ? CustomFieldJsonCodec.DeserialiseValues("""{"Ref":"Edited loss","Points":7,"Due":null}""") : null;

        var result = await ResolveService<CardService>().DuplicateCardAsync(source.BoardId, source.BoardCardId,
            new CreateCardRequest(null, "Converted", source.Description, [], target.Id), ActorUserId, fields: fields);

        Assert.True(result.Success);
        Assert.Equal(supplyFields ? 7 : 5, result.Data!.CustomFieldValues["Points"].GetInt32());
        Assert.Equal(source.Description, result.Data.Description);
        var lossComment = await DbContextForAssert.CardComments.Include(comment => comment.Card).SingleAsync();
        Assert.Contains($"    Card type: {source.CardType.Name} → Target\n", lossComment.Text);
        Assert.Contains(supplyFields ? "Reference: Edited loss" : "Reference: Existing", lossComment.Text);
        Assert.Equal(result.Data.Id, lossComment.Card.BoardCardId);
        Assert.NotEqual(source.Id, lossComment.CardId);
        var unchanged = await DbContextForAssert.Cards.SingleAsync(card => card.Id == source.Id);
        Assert.Equal(source.Description, unchanged.Description);
        Assert.Equal(source.CustomFieldValuesJson, unchanged.CustomFieldValuesJson);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Ref":null,"Points":11,"Due":null}""")]
    [InlineData("""{"Ref":null,"Points":0,"Due":null,"Unknown":1}""")]
    public async Task Duplicate_WithInvalidFields_ShouldNotCreateCardOrRelatedData(string json)
    {
        var source = await ArrangeCardAsync();

        var result = await ResolveService<CardService>().DuplicateCardAsync(source.BoardId, source.BoardCardId,
            new CreateCardRequest(null, "Invalid", "", ["New tag"], source.CardTypeId, SlickName: "New slick"), ActorUserId,
            fields: CustomFieldJsonCodec.DeserialiseValues(json));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(source.CustomFieldValuesJson, (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
        Assert.Empty(await DbContextForAssert.Slicks.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).CardCreatedEvents);
    }

    [Fact]
    public async Task Duplicate_WithFullDescription_ShouldCreateCardAndLossComment()
    {
        var source = await ArrangeCardAsync();
        var target = new EntityCardType { BoardId = source.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().DuplicateCardAsync(source.BoardId, source.BoardCardId,
            new CreateCardRequest(null, "Too long", new string('x', 20_000), [], target.Id), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new string('x', 20_000), result.Data!.Description);
        Assert.Equal(2, await DbContextForAssert.Cards.CountAsync());
        Assert.True((await DbContextForAssert.CardComments.SingleAsync()).IsSystem);
    }

    private async Task<EntityBoardCard> ArrangeCardAsync()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        SetFields(card.CardType, Definitions);
        card.CustomFieldValuesJson = "{\"Ref\":\"Existing\",\"Points\":5,\"Due\":null}";
        card.CardCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        card.CardUpdatedUtc = card.CardCreatedUtc;
        await DbContextForArrange.SaveChangesAsync();
        return card;
    }
}
