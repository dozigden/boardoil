using BoardOil.Abstractions.CustomField;
using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CustomFieldEntityTests : TestBaseDb
{
    [Theory]
    [InlineData("text")]
    [InlineData("integer")]
    [InlineData("date")]
    public async Task Configuration_ShouldRoundTripWithTypeOutsideJsonAndNativeDefault(string kind)
    {
        CustomFieldDefinition definition = kind switch
        {
            "text" => new TextCustomFieldDefinition { Code = "Value", Name = "Value", DefaultValue = "Text", MinLength = 1, MaxLength = 10 },
            "integer" => new IntegerCustomFieldDefinition { Code = "Value", Name = "Value", DefaultValue = 3, Min = 0, Max = 10 },
            _ => new DateCustomFieldDefinition { Code = "Value", Name = "Value", DefaultValue = new DateOnly(2024, 2, 29) }
        };
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, [definition]);
        await DbContextForArrange.SaveChangesAsync();

        var stored = await DbContextForAssert.CustomFields.SingleAsync();

        Assert.Equal(kind, stored.DataType.ToString().ToLowerInvariant());
        Assert.Equal(definition, CustomFieldConfigurationCodec.Read(stored));
        using var json = JsonDocument.Parse(stored.ConfigurationJson);
        Assert.False(json.RootElement.TryGetProperty("type", out _));
        var cardValue = CustomFieldRules.CreateValues([definition], null).Value!["Value"];
        Assert.Equal(cardValue.GetRawText(), json.RootElement.GetProperty("defaultValue").GetRawText());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_ShouldRejectDuplicateCodesAndNamesIgnoringCase(bool duplicateCode)
    {
        CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Référence", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();
        var duplicate = new EntityCustomField { BoardId = type.BoardId };
        CustomFieldConfigurationCodec.Write(duplicate, new TextCustomFieldDefinition
        {
            Code = duplicateCode ? "REF" : "Other",
            Name = duplicateCode ? "Other" : "RÉFÉRENCE",
            Nullable = true
        });
        DbContextForArrange.CustomFields.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContextForArrange.SaveChangesAsync());

        Assert.Single(await DbContextForAssert.CustomFields.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_ShouldRejectCrossBoardAssignments(bool useForeignBoardId)
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var other = CreateBoard("Other").AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == board.BoardId);
        var foreign = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == other.BoardId);
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();
        var field = await DbContextForArrange.CustomFields.SingleAsync();
        // Use a fresh context to exercise the database constraint without navigation fix-up.
        await using var db = CreateDbContextForAct();
        db.CustomFieldCardTypes.Add(new EntityCustomFieldCardType
        {
            BoardId = useForeignBoardId ? foreign.BoardId : type.BoardId,
            CustomFieldId = field.Id, CardTypeId = foreign.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Single(await DbContextForAssert.CustomFieldCardTypes.ToListAsync());
    }

    [Fact]
    public async Task Database_ShouldRejectDuplicateAssignments()
    {
        CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();
        var field = await DbContextForArrange.CustomFields.SingleAsync();
        DbContextForArrange.CustomFieldCardTypes.Add(new EntityCustomFieldCardType
        {
            BoardId = type.BoardId, CustomFieldId = field.Id, CardTypeId = type.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContextForArrange.SaveChangesAsync());

        Assert.Single(await DbContextForAssert.CustomFieldCardTypes.ToListAsync());
    }

    [Fact]
    public async Task Edit_ShouldKeepFieldAndUnchangedAssignmentIdentities()
    {
        CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        var definition = new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true };
        SetFields(type, [definition]);
        await DbContextForArrange.SaveChangesAsync();
        var originalField = await DbContextForArrange.CustomFields.SingleAsync();
        var originalAssignment = await DbContextForArrange.CustomFieldCardTypes.SingleAsync();
        var input = CustomFieldJsonCodec.DeserialiseDefinitionInputs(CustomFieldJsonCodec.SerialiseDefinitions([definition with { Name = "Renamed" }]))[0];

        var result = await ResolveService<ICustomFieldService>().UpdateAsync(type.BoardId, "Ref",
            new(input, new SelectedCardTypes([type.Id])), ActorUserId);

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.CustomFields.SingleAsync();
        Assert.Equal(originalField.Id, stored.Id);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal(originalAssignment.Id, (await DbContextForAssert.CustomFieldCardTypes.SingleAsync()).Id);
    }

    [Fact]
    public async Task DeleteBoard_ShouldCascadeFieldsAndAssignments()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var type = await DbContextForArrange.CardTypes.SingleAsync();
        SetFields(type, [new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true }]);
        await DbContextForArrange.SaveChangesAsync();
        await using var db = CreateDbContextForAct();

        await db.Boards.Where(item => item.Id == board.BoardId).ExecuteDeleteAsync();

        Assert.Empty(await DbContextForAssert.CustomFields.ToListAsync());
        Assert.Empty(await DbContextForAssert.CustomFieldCardTypes.ToListAsync());
    }
}
