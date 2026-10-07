using System.Text.Json;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CardField;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardFieldArchiveTests : TestBaseDb
{
    private static readonly CardFieldDefinition[] Definitions =
    [
        new TextFieldDefinition { FieldCode = "Ref", DisplayName = "Reference", Nullable = true, Emoji = "🔖" },
        new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 1 },
        new DateFieldDefinition { FieldCode = "Due", DisplayName = "Due date", Nullable = true },
        new TextFieldDefinition { FieldCode = "Empty", DisplayName = "Empty", Nullable = true }
    ];
    private const string Values = """{"Ref":"Saved","Points":0,"Due":"2024-02-29","Empty":null}""";

    [Fact]
    public async Task Archive_Read_ShouldPreserveCapturedDefinitionsAndValuesAfterTypeEdits()
    {
        var source = await ArrangeCardAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        source.CardType.FieldDefinitionsJson = "[]";
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.GetArchivedCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Values, CardFieldJsonCodec.SerialiseValues(result.Data!.Card.Fields));
        Assert.Equal(Definitions, result.Data.FieldDefinitions);
        var stored = await DbContextForAssert.ArchivedCards.SingleAsync();
        using var snapshot = JsonDocument.Parse(stored.SnapshotJson);
        Assert.Equal(1, snapshot.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(4, snapshot.RootElement.GetProperty("payload").GetProperty("fieldDefinitions").GetArrayLength());
        Assert.Equal(Values, snapshot.RootElement.GetProperty("payload").GetProperty("fields").GetRawText());
    }

    [Fact]
    public async Task Restore_WithUnchangedType_ShouldRoundTripValuesWithoutLossRecord()
    {
        var source = await ArrangeCardAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Values, CardFieldJsonCodec.SerialiseValues(result.Data!.Fields));
        Assert.Equal(source.Description, result.Data.Description);
        Assert.Equal(Values, (await DbContextForAssert.Cards.SingleAsync()).FieldValuesJson);
        Assert.Empty(await DbContextForAssert.ArchivedCards.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_WithChangedDefinitions_ShouldConvertAndRecordLosses(bool tightenLimit)
    {
        var source = await ArrangeCardAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        CardFieldDefinition points = new TextFieldDefinition { FieldCode = "Points", DisplayName = "Renamed", Nullable = true };
        if (tightenLimit)
        {
            points = new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Renamed", DefaultValue = 1, Min = 1 };
        }
        source.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [points, new TextFieldDefinition { FieldCode = "New", DisplayName = "New", Nullable = true, DefaultValue = "Default" }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        var fields = result.Data!.Fields;
        if (tightenLimit) Assert.Equal(1, fields["Points"].GetInt32());
        else Assert.Equal("0", fields["Points"].GetString());
        Assert.Equal("Default", fields["New"].GetString());
        Assert.Equal(2, fields.Count);
        Assert.Equal(source.Description, result.Data.Description);
        var lossComment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.True(lossComment.IsSystem);
        Assert.Contains("Reference: Saved", lossComment.Text);
        Assert.Contains("Due date: 2024-02-29", lossComment.Text);
        Assert.Contains($"    Card type: {source.CardType.Name} → {source.CardType.Name}\n", lossComment.Text);
        Assert.Equal(tightenLimit, lossComment.Text.Contains("Estimate: 0"));
        Assert.DoesNotContain("Empty:", lossComment.Text);
    }

    [Fact]
    public async Task Restore_WhenTypeWasDeleted_ShouldMatchFallbackTypeByDisplayName()
    {
        var source = await ArrangeCardAsync();
        var systemType = source.CardType;
        var originalType = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Removed type", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = systemType.FieldDefinitionsJson
        };
        source.CardType = originalType;
        DbContextForArrange.CardTypes.Add(originalType);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        DbContextForArrange.Entry(source).State = EntityState.Detached;
        DbContextForArrange.CardTypes.Remove(originalType);
        systemType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [new TextFieldDefinition { FieldCode = "DifferentCode", DisplayName = "estimate", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(systemType.Id, result.Data!.CardTypeId);
        Assert.Equal("0", Assert.Single(result.Data.Fields).Value.GetString());
        Assert.Equal(source.Description, result.Data.Description);
        var lossComment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.True(lossComment.IsSystem);
        Assert.Contains("Reference: Saved", lossComment.Text);
        Assert.DoesNotContain("Estimate: 0", lossComment.Text);
        Assert.Contains($"    Card type: Removed type → {systemType.Name}\n", lossComment.Text);
    }

    [Fact]
    public async Task Restore_WhenTypeIsRecreatedWithSameName_ShouldMatchFieldsByDisplayName()
    {
        var source = await ArrangeCardAsync();
        var originalType = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Estimation", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
                [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 0 }])
        };
        DbContextForArrange.CardTypes.Add(originalType);
        source.CardType = originalType;
        source.FieldValuesJson = """{"Points":7}""";
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        DbContextForArrange.Entry(source).State = EntityState.Detached;
        DbContextForArrange.CardTypes.Remove(originalType);
        await DbContextForArrange.SaveChangesAsync();
        var replacement = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Estimation", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [
                new IntegerFieldDefinition { FieldCode = "Effort", DisplayName = "Estimate", DefaultValue = 0 },
                new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Budget", DefaultValue = 0 }
            ])
        };
        DbContextForArrange.CardTypes.Add(replacement);
        await DbContextForArrange.SaveChangesAsync();
        Assert.NotEqual(originalType.Id, replacement.Id);

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(replacement.Id, result.Data!.CardTypeId);
        Assert.Equal(7, result.Data.Fields["Effort"].GetInt32());
        Assert.Equal(0, result.Data.Fields["Points"].GetInt32());
        Assert.Equal(source.Description, result.Data.Description);
    }

    [Fact]
    public async Task Restore_WithFullDescription_ShouldPreserveDescriptionAndRecordComment()
    {
        var source = await ArrangeCardAsync();
        source.Description = new string('x', 20_000);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        source.CardType.FieldDefinitionsJson = "[]";
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(source.Description, result.Data!.Description);
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
        Assert.Empty(await DbContextForAssert.ArchivedCards.ToListAsync());
        Assert.True((await DbContextForAssert.CardComments.SingleAsync()).IsSystem);
    }

    [Fact]
    public async Task Restore_LegacySnapshotWithoutFields_ShouldUseCurrentDefaults()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(Definitions);
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 99, SearchTitle = "Legacy", SearchTagsJson = "[]",
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = $$"""
                {"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-26T12:00:00Z","payload":{
                "boardId":{{board.BoardId}},"originalCardId":99,"boardColumnId":{{board.GetColumn("Todo").Id}},
                "originalColumnName":"Todo","cardTypeId":{{type.Id}},"cardTypeName":"{{type.Name}}",
                "title":"Legacy","description":"Desc","sortKey":"A","tags":[],"tagNames":[],
                "createdAtUtc":"2026-04-26T12:00:00Z","updatedAtUtc":"2026-04-26T12:00:00Z"} }
                """
        });
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 99, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.Data!.Fields["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Data.Fields["Ref"].ValueKind);
        Assert.Equal("Desc", result.Data.Description);
    }

    private async Task<EntityBoardCard> ArrangeCardAsync()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        card.CardType.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(Definitions);
        card.FieldValuesJson = Values;
        await DbContextForArrange.SaveChangesAsync();
        return card;
    }
}
