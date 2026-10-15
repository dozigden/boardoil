using System.Text.Json;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldArchiveTests : TestBaseDb
{
    private static readonly CustomFieldDefinition[] Definitions =
    [
        new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true, Emoji = "🔖" },
        new IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", DefaultValue = 1 },
        new DateCustomFieldDefinition { Code = "Due", Name = "Due date", Nullable = true },
        new TextCustomFieldDefinition { Code = "Empty", Name = "Empty", Nullable = true }
    ];
    private const string Values = """{"Ref":"Saved","Points":0,"Due":"2024-02-29","Empty":null}""";

    [Fact]
    public async Task Archive_Read_ShouldPreserveCapturedDefinitionsAndValuesAfterTypeEdits()
    {
        var source = await ArrangeCardAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        SetFields(source.CardType, []);
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.GetArchivedCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Values, CustomFieldJsonCodec.SerialiseValues(result.Data!.Card.CustomFieldValues));
        Assert.Equal(Definitions, result.Data.CustomFieldDefinitions);
        var stored = await DbContextForAssert.ArchivedCards.SingleAsync();
        using var snapshot = JsonDocument.Parse(stored.SnapshotJson);
        Assert.Equal(1, snapshot.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(4, snapshot.RootElement.GetProperty("payload").GetProperty("customFieldDefinitions").GetArrayLength());
        Assert.Equal(Values, snapshot.RootElement.GetProperty("payload").GetProperty("customFieldValues").GetRawText());
    }

    [Fact]
    public async Task Restore_WithUnchangedType_ShouldRoundTripValuesWithoutLossRecord()
    {
        var source = await ArrangeCardAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Values, CustomFieldJsonCodec.SerialiseValues(result.Data!.CustomFieldValues));
        Assert.Equal(source.Description, result.Data.Description);
        Assert.Equal(Values, (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
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
        CustomFieldDefinition points = new TextCustomFieldDefinition { Code = "Points", Name = "Renamed", Nullable = true };
        if (tightenLimit)
        {
            points = new IntegerCustomFieldDefinition { Code = "Points", Name = "Renamed", DefaultValue = 1, Min = 1 };
        }
        SetFields(source.CardType,
            [points, new TextCustomFieldDefinition { Code = "New", Name = "New", Nullable = true, DefaultValue = "Default" }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        var fields = result.Data!.CustomFieldValues;
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
    public async Task Restore_WhenTypeWasDeleted_ShouldMatchFallbackFieldsByCode()
    {
        var source = await ArrangeCardAsync();
        var systemType = source.CardType;
        var originalType = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Removed type", StyleName = "auto", StylePropertiesJson = "{}",
        };
        source.CardType = originalType;
        DbContextForArrange.CardTypes.Add(originalType);
        await DbContextForArrange.SaveChangesAsync();
        SetFields(originalType, systemType.Board.CustomFields.GetApplicableCustomFieldDefinitions(systemType.Id));
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        DbContextForArrange.Entry(source).State = EntityState.Detached;
        DbContextForArrange.CardTypes.Remove(originalType);
        SetFields(systemType,
            [new TextCustomFieldDefinition { Code = "Points", Name = "estimate", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(systemType.Id, result.Data!.CardTypeId);
        Assert.Equal("0", Assert.Single(result.Data.CustomFieldValues).Value.GetString());
        Assert.Equal(source.Description, result.Data.Description);
        var lossComment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.True(lossComment.IsSystem);
        Assert.Contains("Reference: Saved", lossComment.Text);
        Assert.DoesNotContain("Estimate: 0", lossComment.Text);
        Assert.Contains($"    Card type: Removed type → {systemType.Name}\n", lossComment.Text);
    }

    [Fact]
    public async Task Restore_WhenTypeIsRecreatedWithSameName_ShouldMatchFieldsByCode()
    {
        var source = await ArrangeCardAsync();
        var originalType = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Estimation", StyleName = "auto", StylePropertiesJson = "{}",
        };
        DbContextForArrange.CardTypes.Add(originalType);
        source.CardType = originalType;
        source.CustomFieldValuesJson = """{"Points":7}""";
        await DbContextForArrange.SaveChangesAsync();
        SetFields(originalType,
                [new IntegerCustomFieldDefinition { Code = "Points", Name = "Estimate", DefaultValue = 0 }]);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();
        Assert.True((await service.ArchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId)).Success);
        DbContextForArrange.Entry(source).State = EntityState.Detached;
        DbContextForArrange.CardTypes.Remove(originalType);
        await DbContextForArrange.SaveChangesAsync();
        var replacement = new EntityCardType
        {
            BoardId = source.BoardId, Name = "Estimation", StyleName = "auto", StylePropertiesJson = "{}",
        };
        DbContextForArrange.CardTypes.Add(replacement);
        await DbContextForArrange.SaveChangesAsync();
        SetFields(replacement,
            [
                new IntegerCustomFieldDefinition { Code = "Effort", Name = "Estimate", DefaultValue = 0 },
                new IntegerCustomFieldDefinition { Code = "Points", Name = "Budget", DefaultValue = 0 }
            ]);
        await DbContextForArrange.SaveChangesAsync();
        Assert.NotEqual(originalType.Id, replacement.Id);

        var result = await service.UnarchiveCardAsync(source.BoardId, source.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(replacement.Id, result.Data!.CardTypeId);
        Assert.Equal(0, result.Data.CustomFieldValues["Effort"].GetInt32());
        Assert.Equal(7, result.Data.CustomFieldValues["Points"].GetInt32());
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
        SetFields(source.CardType, []);
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
        SetFields(type, Definitions);
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
        Assert.Equal(1, result.Data!.CustomFieldValues["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, result.Data.CustomFieldValues["Ref"].ValueKind);
        Assert.Equal("Desc", result.Data.Description);
    }

    private async Task<EntityBoardCard> ArrangeCardAsync()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        SetFields(card.CardType, Definitions);
        card.CustomFieldValuesJson = Values;
        await DbContextForArrange.SaveChangesAsync();
        return card;
    }
}
