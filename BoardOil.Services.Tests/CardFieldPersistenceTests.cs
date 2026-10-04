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

public sealed class CardFieldPersistenceTests : TestBaseDb
{
    private static readonly CardFieldDefinition[] Definitions =
    [
        new TextFieldDefinition { FieldCode = "Ref", DisplayName = "Reference", Nullable = true, DefaultValue = "Default" },
        new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Points", DefaultValue = 0, Min = 0, Max = 10 },
        new DateFieldDefinition { FieldCode = "Due", DisplayName = "Due date", Nullable = true }
    ];

    [Fact]
    public async Task Create_ShouldPersistAndPublishDefaultsWithoutChangingCreationContract()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(Definitions);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().CreateCardAsync(board.BoardId,
            new CreateCardRequest(null, "Created", null, null), ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":0,\"Due\":null}", stored.FieldValuesJson);
        Assert.Equal("Default", result.Data!.Fields["Ref"].GetString());
        Assert.Equal(0, result.Data.Fields["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Data.Fields["Due"].ValueKind);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(stored.FieldValuesJson, CardFieldJsonCodec.SerialiseValues(Assert.Single(events.CardCreatedEvents).Card.Fields));
    }

    [Fact]
    public async Task Update_WhenFieldsOmitted_ShouldPreserveStoredValues()
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest("Renamed", card.Description, [], card.CardTypeId);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.FieldValuesJson, stored.FieldValuesJson);
        Assert.Equal("Renamed", stored.Title);
        Assert.Equal(5, result.Data!.Fields["Points"].GetInt32());
    }

    [Fact]
    public async Task Update_WhenOnlyFieldsChange_ShouldPersistNormalisedValuesAndPublishNewTimestamp()
    {
        var card = await ArrangeCardAsync();
        var values = CardFieldJsonCodec.DeserialiseValues("{\"ref\":\"  Changed  \",\"points\":7,\"due\":\"2024-02-29\"}");
        var request = new UpdateCardRequest(card.Title, card.Description, [], card.CardTypeId, Fields: values);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Ref\":\"Changed\",\"Points\":7,\"Due\":\"2024-02-29\"}", stored.FieldValuesJson);
        Assert.True(stored.CardUpdatedUtc > card.CardUpdatedUtc);
        Assert.Equal(card.CardCreatedUtc, stored.CardCreatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(7, Assert.Single(events.CardUpdatedEvents).Card.Fields["Points"].GetInt32());
        Assert.Equal("Description", stored.Description);
    }

    [Fact]
    public async Task Update_WhenValuesUnchanged_ShouldPreserveLogicalTimestamp()
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest(card.Title, card.Description, [], card.CardTypeId,
            Fields: CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(card.CardUpdatedUtc, result.Data!.CardUpdatedUtc);
    }

    [Theory]
    [InlineData("{\"Ref\":null,\"Points\":11,\"Due\":null}", "fields.Points")]
    [InlineData("{\"Ref\":null,\"Points\":0}", "fields.Due")]
    [InlineData("{\"Ref\":null,\"Points\":0,\"Due\":null,\"Unknown\":null}", "fields.Unknown")]
    public async Task Update_WhenFieldsInvalid_ShouldLeaveCardAndRelatedDataUnchanged(string json, string property)
    {
        var card = await ArrangeCardAsync();
        var request = new UpdateCardRequest("Do not save", "Changed description", ["New tag"], card.CardTypeId,
            SlickName: "New slick", Fields: CardFieldJsonCodec.DeserialiseValues(json));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.False(result.Success);
        Assert.Contains(property, result.ValidationErrors!.Keys);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.Title, stored.Title);
        Assert.Equal(card.Description, stored.Description);
        Assert.Equal(card.FieldValuesJson, stored.FieldValuesJson);
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
        Assert.Empty(await DbContextForAssert.Slicks.ToListAsync());
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.CardUpdatedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypeChange_ShouldApplyOriginalFieldEditsBeforeConversionAndAppendEditedLosses(bool supplyValues)
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        {
            BoardId = card.BoardId, Name = "Target", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
                [new TextFieldDefinition { FieldCode = "Estimate", DisplayName = "POINTS", Nullable = true }])
        };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        IReadOnlyDictionary<string, JsonElement>? fields = null;
        if (supplyValues)
        {
            fields = CardFieldJsonCodec.DeserialiseValues("{\"Ref\":\"Edited reference\",\"Points\":7,\"Due\":null}");
        }
        var request = new UpdateCardRequest(card.Title, "Edited description", [], target.Id, Fields: fields);

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(target.Id, stored.CardTypeId);
        Assert.Equal(supplyValues ? "7" : "5", result.Data!.Fields["Estimate"].GetString());
        Assert.Single(result.Data.Fields);
        Assert.Equal("Edited description\n\nFields removed during card type change:\n\n- Reference: "
            + (supplyValues ? "Edited reference" : "Existing"), stored.Description);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Theory]
    [InlineData("{\"Ref\":null,\"Points\":11,\"Due\":null}")]
    [InlineData("{}")]
    public async Task TypeChange_WhenSuppliedFieldsInvalid_ShouldNotSaveConversionOrHistory(string json)
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType { BoardId = card.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();
        var request = new UpdateCardRequest(card.Title, card.Description, [], target.Id,
            Fields: CardFieldJsonCodec.DeserialiseValues(json));

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId, request, ActorUserId);

        Assert.False(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CardTypeId, stored.CardTypeId);
        Assert.Equal(card.Description, stored.Description);
        Assert.Equal(card.FieldValuesJson, stored.FieldValuesJson);
    }

    [Fact]
    public async Task TypeChange_WithoutLosses_ShouldPreserveDescription()
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType
        {
            BoardId = card.BoardId, Name = "Same fields", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = card.CardType.FieldDefinitionsJson
        };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, card.Description, [], target.Id), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(card.Description, result.Data!.Description);
        Assert.Equal(card.FieldValuesJson, CardFieldJsonCodec.SerialiseValues(result.Data.Fields));
    }

    [Fact]
    public async Task TypeChange_WhenHistoryExceedsDescriptionLimit_ShouldLeaveCardUnchanged()
    {
        var card = await ArrangeCardAsync();
        var target = new EntityCardType { BoardId = card.BoardId, Name = "Empty", StyleName = "auto", StylePropertiesJson = "{}" };
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().UpdateCardAsync(card.BoardId, card.BoardCardId,
            new UpdateCardRequest(card.Title, new string('a', 20_000), [], target.Id), ActorUserId);

        Assert.False(result.Success);
        Assert.Contains("description", result.ValidationErrors!.Keys);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CardTypeId, stored.CardTypeId);
        Assert.Equal(card.FieldValuesJson, stored.FieldValuesJson);
        Assert.Equal(card.Description, stored.Description);
    }

    [Fact]
    public async Task TypeChange_WhenLostTextLooksLikeMarkdown_ShouldEscapeItAndAllowSubsequentEdits()
    {
        var card = await ArrangeCardAsync();
        const string text = "![image](data:image/png;base64,AAAA)";
        card.FieldValuesJson = CardFieldJsonCodec.SerialiseValues(new Dictionary<string, JsonElement>
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
        Assert.Contains(@"\!\[image\]\(data:image/png;base64,AAAA\)", result.Data!.Description);
    }

    private async Task<EntityBoardCard> ArrangeCardAsync()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(Definitions);
        card.FieldValuesJson = "{\"Ref\":\"Existing\",\"Points\":5,\"Due\":null}";
        card.CardCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        card.CardUpdatedUtc = card.CardCreatedUtc;
        await DbContextForArrange.SaveChangesAsync();
        return card;
    }
}
