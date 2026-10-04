using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Services.Card;
using BoardOil.Services.CardField;
using BoardOil.Services.CardType;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardTypeFieldManagementTests : TestBaseDb
{
    private static TextFieldDefinition Text => new() { FieldCode = "Ref", DisplayName = "Reference", Nullable = true };
    private static IntegerFieldDefinition Integer => new() { FieldCode = "Points", DisplayName = "Points", DefaultValue = 0 };
    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

    [Fact]
    public async Task Create_ShouldNormalisePersistAndExposeOrderedDefinitions()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var request = new CreateCardTypeRequest("Custom", Fields:
            [Text with { DisplayName = " Customer reference ", DefaultValue = " Unknown ", Emoji = "📎", ShowOnCard = true }, Integer]);

        var result = await ResolveService<CardTypeService>().CreateCardTypeAsync(board.BoardId, request, ActorUserId);

        Assert.True(result.Success);
        var fields = result.Data!.Fields;
        Assert.Equal(new[] { "Ref", "Points" }, fields.Select(field => field.FieldCode));
        var text = Assert.IsType<TextFieldDefinition>(fields[0]);
        Assert.Equal("Customer reference", text.DisplayName);
        Assert.Equal("Unknown", text.DefaultValue);
        Assert.True(text.ShowOnCard);
        var stored = await DbContextForAssert.CardTypes.SingleAsync(type => type.Id == result.Data.Id);
        Assert.Equal(fields, CardFieldJsonCodec.DeserialiseDefinitions(stored.FieldDefinitionsJson));
        Assert.Equal([board.BoardId], Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Create_WhenDefinitionsInvalid_ShouldNotCreateType()
    {
        var board = CreateBoard().AddColumn("Todo").Build();

        var result = await ResolveService<CardTypeService>().CreateCardTypeAsync(board.BoardId,
            new CreateCardTypeRequest("Invalid", Fields: [Text with { Nullable = false }]), ActorUserId);

        Assert.False(result.Success);
        Assert.Contains("fields[0].defaultValue", result.ValidationErrors!.Keys);
        Assert.Single(await DbContextForAssert.CardTypes.ToListAsync());
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Update_WhenFieldsOmitted_ShouldPreserveDefinitionsAndValues()
    {
        var type = await ArrangeTypeAsync([Text], "{\"Ref\":\"Existing\"}");

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest("Renamed"), ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.CardTypes.SingleAsync();
        Assert.Equal(type.FieldDefinitionsJson, stored.FieldDefinitionsJson);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal("{\"Ref\":\"Existing\"}", (await DbContextForAssert.Cards.SingleAsync()).FieldValuesJson);
    }

    [Fact]
    public async Task Update_AddingFields_ShouldPopulateAllExistingCardsAccordingToNullability()
    {
        var type = await ArrangeTypeAsync([], "{}", "{}");
        var other = CreateBoard("Other board").AddColumn("Todo").AddCard("Other").Build();
        var additions = new CardFieldDefinition[]
        {
            Text with { DefaultValue = "Only for new cards" }, Integer with { DefaultValue = 3 },
            new DateFieldDefinition { FieldCode = "Due", DisplayName = "Due", DefaultValue = new DateOnly(2026, 1, 1) }
        };

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest(type.Name, Fields: additions), ActorUserId);

        Assert.True(result.Success);
        var cards = await DbContextForAssert.Cards.Where(card => card.BoardId == type.BoardId).ToListAsync();
        Assert.Equal(2, cards.Count);
        Assert.All(cards, card =>
        {
            Assert.Equal("{\"Ref\":null,\"Points\":3,\"Due\":\"2026-01-01\"}", card.FieldValuesJson);
            Assert.Equal("Description", card.Description);
            Assert.True(card.CardUpdatedUtc > card.CardCreatedUtc);
        });
        Assert.Equal("{}", (await DbContextForAssert.Cards.SingleAsync(card => card.BoardId == other.BoardId)).FieldValuesJson);
        Assert.Equal([type.BoardId], Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Update_PresentationOrderAndDefaults_ShouldPreserveValuesAndLogicalTimestamps()
    {
        var type = await ArrangeTypeAsync([Text, Integer], "{\"Ref\":\"Existing\",\"Points\":5}");
        var fields = new CardFieldDefinition[]
        {
            Integer with { DefaultValue = 7 },
            Text with { DisplayName = "Renamed", Emoji = "📎", ShowOnCard = true, DefaultValue = "New default" }
        };

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest(type.Name, Fields: fields), ActorUserId);

        Assert.True(result.Success);
        var card = await DbContextForAssert.Cards.SingleAsync();
        var values = CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson);
        Assert.Equal("Existing", values["Ref"].GetString());
        Assert.Equal(5, values["Points"].GetInt32());
        Assert.Equal(card.CardCreatedUtc, card.CardUpdatedUtc);
        Assert.Equal("Description", card.Description);
        Assert.Equal(fields, result.Data!.Fields);
    }

    [Fact]
    public async Task Update_MakingFieldRequired_ShouldFillNullsAndKeepPopulatedValues()
    {
        var type = await ArrangeTypeAsync([Text], "{\"Ref\":null}", "{\"Ref\":\"Existing\"}");

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest(type.Name, Fields: [Text with { Nullable = false, DefaultValue = "Required" }]), ActorUserId);

        Assert.True(result.Success);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.Equal("{\"Ref\":\"Required\"}", cards[0].FieldValuesJson);
        Assert.Equal("{\"Ref\":\"Existing\"}", cards[1].FieldValuesJson);
        Assert.All(cards, card => Assert.Equal("Description", card.Description));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_TighteningLimits_ShouldRejectAllChangesIfAnyCardBecomesInvalid(bool text)
    {
        CardFieldDefinition original = Integer;
        CardFieldDefinition updated = Integer with { Max = 5 };
        string[] values = ["{\"Points\":3}", "{\"Points\":9}"];
        if (text)
        {
            original = Text;
            updated = Text with { MaxLength = 5 };
            values = ["{\"Ref\":\"Short\"}", "{\"Ref\":\"Long value\"}"];
        }
        var type = await ArrangeTypeAsync([original], values);

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest("Do not rename", Fields: [updated, Text with { FieldCode = "New", DisplayName = "New", Nullable = false, DefaultValue = "Added" }]), ActorUserId);

        Assert.False(result.Success);
        Assert.Contains(result.ValidationErrors!.Keys, key => key.StartsWith("cards[2].fields."));
        var storedType = await DbContextForAssert.CardTypes.SingleAsync();
        Assert.Equal(type.Name, storedType.Name);
        Assert.Equal(type.FieldDefinitionsJson, storedType.FieldDefinitionsJson);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.Equal(values, cards.Select(card => card.FieldValuesJson));
        Assert.All(cards, card => Assert.Equal("Description", card.Description));
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Update_ChangingKind_ShouldConvertValuesAndRecordOnlyFailedConversions()
    {
        var type = await ArrangeTypeAsync([Text], "{\"Ref\":\"12\"}", "{\"Ref\":\"Not a number\"}");
        var target = Integer with { FieldCode = "Ref", DisplayName = "Renamed", DefaultValue = 0 };

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest(type.Name, Fields: [target]), ActorUserId);

        Assert.True(result.Success);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.Equal("{\"Ref\":12}", cards[0].FieldValuesJson);
        Assert.Equal("Description", cards[0].Description);
        Assert.Equal("{\"Ref\":0}", cards[1].FieldValuesJson);
        Assert.EndsWith("- Reference: Not a number", cards[1].Description);
        Assert.DoesNotContain("Renamed:", cards[1].Description);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Fact]
    public async Task Update_EmptyArray_ShouldRemoveAllFieldsAndKeepLostValuesInDescriptions()
    {
        var type = await ArrangeTypeAsync([Text, Integer], "{\"Ref\":\"Existing\",\"Points\":0}");

        var result = await ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest(type.Name, Fields: []), ActorUserId);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Fields);
        var card = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{}", card.FieldValuesJson);
        Assert.Equal("Description\n\nFields removed during field definition change:\n\n- Reference: Existing\n- Points: 0", card.Description);
    }

    [Fact]
    public async Task Update_WhenDatabaseWriteFails_ShouldRollbackDefinitionsValuesAndHistory()
    {
        var type = await ArrangeTypeAsync([Text], "{\"Ref\":\"First\"}", "{\"Ref\":\"Second\"}");
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectFieldChange BEFORE UPDATE OF "FieldValuesJson" ON "Cards"
            WHEN NEW."BoardCardId" = 2
            BEGIN SELECT RAISE(ABORT, 'Injected field failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => ResolveService<CardTypeService>().UpdateCardTypeAsync(type.BoardId, type.Id,
            new UpdateCardTypeRequest("Do not rename", Fields: []), ActorUserId));

        var storedType = await DbContextForAssert.CardTypes.SingleAsync();
        Assert.Equal(type.Name, storedType.Name);
        Assert.Equal(type.FieldDefinitionsJson, storedType.FieldDefinitionsJson);
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.Equal(new[] { "{\"Ref\":\"First\"}", "{\"Ref\":\"Second\"}" }, cards.Select(card => card.FieldValuesJson));
        Assert.All(cards, card => Assert.Equal("Description", card.Description));
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Delete_ShouldConvertCardsToDefaultTypeAndRecordLosses()
    {
        var type = await ArrangeTypeAsync([Text, Integer], "{\"Ref\":\"Lost\",\"Points\":5}");
        type.IsSystem = false;
        var target = new EntityCardType
        {
            BoardId = type.BoardId, Name = "Default", IsSystem = true, StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions([Text with { FieldCode = "Effort", DisplayName = "POINTS" }])
        };
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.CardTypes.Add(target);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardTypeService>().DeleteCardTypeAsync(type.BoardId, type.Id, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(target.Id, (await DbContextForAssert.CardTypes.SingleAsync()).Id);
        var card = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(target.Id, card.CardTypeId);
        Assert.Equal("{\"Effort\":\"5\"}", card.FieldValuesJson);
        Assert.EndsWith("- Reference: Lost", card.Description);
        Assert.True(card.CardUpdatedUtc > card.CardCreatedUtc);
    }

    [Fact]
    public async Task CreateCard_ShouldReadCurrentDefinitionsEvenIfTypeWasAlreadyTracked()
    {
        var type = await ArrangeTypeAsync([Integer]);
        using var scope = ResolveService<IDbContextScopeFactory>().Create();
        var tracked = await ResolveService<ICardTypeRepository>().GetByIdInBoardAsync(type.BoardId, type.Id);
        Assert.Equal(type.FieldDefinitionsJson, tracked!.FieldDefinitionsJson);
        type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions([Integer with { DefaultValue = 7 }, Text]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<CardService>().CreateCardAsync(type.BoardId,
            new CreateCardRequest(null, "Created", "", [], type.Id), ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal("{\"Points\":7,\"Ref\":null}", stored.FieldValuesJson);
    }

    [Fact]
    public async Task Delete_WhenDatabaseWriteFails_ShouldKeepTypeAndAllOriginalCards()
    {
        var type = await ArrangeTypeAsync([Text], "{\"Ref\":\"First\"}", "{\"Ref\":\"Second\"}");
        type.IsSystem = false;
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = type.BoardId, Name = "Default", IsSystem = true, StyleName = "auto", StylePropertiesJson = "{}"
        });
        await DbContextForArrange.SaveChangesAsync();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectFieldReassignment BEFORE UPDATE OF "CardTypeId" ON "Cards"
            WHEN NEW."BoardCardId" = 2
            BEGIN SELECT RAISE(ABORT, 'Injected reassignment failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => ResolveService<CardTypeService>().DeleteCardTypeAsync(type.BoardId, type.Id, ActorUserId));

        Assert.True(await DbContextForAssert.CardTypes.AnyAsync(item => item.Id == type.Id));
        var cards = await DbContextForAssert.Cards.OrderBy(card => card.BoardCardId).ToListAsync();
        Assert.All(cards, card => Assert.Equal(type.Id, card.CardTypeId));
        Assert.All(cards, card => Assert.Equal("Description", card.Description));
        Assert.Equal(new[] { "{\"Ref\":\"First\"}", "{\"Ref\":\"Second\"}" }, cards.Select(card => card.FieldValuesJson));
        Assert.Empty(Events.ResyncRequestedBoardIds);
    }

    private async Task<EntityCardType> ArrangeTypeAsync(IReadOnlyList<CardFieldDefinition> fields, params string[] values)
    {
        var builder = CreateBoard().AddColumn("Todo");
        for (var index = 0; index < values.Length; index++)
        {
            builder.AddCard($"Card {index}", "Description");
        }
        var board = builder.Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync(item => item.BoardId == board.BoardId);
        type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(fields);
        for (var index = 0; index < values.Length; index++)
        {
            var card = board.GetCard($"Card {index}");
            card.FieldValuesJson = values[index];
            card.CardCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            card.CardUpdatedUtc = card.CardCreatedUtc;
        }
        await DbContextForArrange.SaveChangesAsync();
        return type;
    }
}
