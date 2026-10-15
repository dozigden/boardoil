using BoardOil.Abstractions.CustomField;
using System.Text.Json;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
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

public sealed class CustomFieldManagementTests : TestBaseDb
{
    private ICustomFieldService Service => ResolveService<ICustomFieldService>();
    private static IntegerCustomFieldDefinition Points => new() { Code = "Points", Name = "Points", DefaultValue = 0 };
    private static CustomFieldRequest Input(CustomFieldDefinition definition, CustomFieldApplicability? applies = null) =>
        new(ToCustomFieldInput(definition), applies ?? new AllCardTypes());

    [Fact]
    public async Task Create_AllTypes_ShouldFillRequiredDefaultsAndApplyToFutureTypes()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Existing").Build();
        var result = await Service.CreateAsync(board.BoardId, Input(Points with { DefaultValue = 3 }), ActorUserId);
        var newType = await ResolveService<CardTypeService>().CreateCardTypeAsync(board.BoardId, new("Future"), ActorUserId);
        var created = await ResolveService<CardService>().CreateCardAsync(board.BoardId, new(null, "New", "", [], newType.Data!.Id), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.IsType<AllCardTypes>(Assert.Single(result.Data!).AppliesTo);
        Assert.Equal(3, created.Data!.CustomFieldValues["Points"].GetInt32());
        Assert.Equal("{\"Points\":3}", (await DbContextForAssert.Cards.SingleAsync(card => card.Title == "Existing")).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Fact]
    public async Task NullableAddition_ShouldStartExistingCardsNullButNewCardsUseDefault()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Existing").Build();
        var input = Input(new TextCustomFieldDefinition { Code = "Ref", Name = " Reference ", Nullable = true, DefaultValue = " Default " });
        var saved = await Service.CreateAsync(board.BoardId, input, ActorUserId);
        var created = await ResolveService<CardService>().CreateCardAsync(board.BoardId, new(null, "New", "", []), ActorUserId);

        Assert.True(saved.Success);
        Assert.Equal("Reference", saved.Data![0].Definition.Name);
        Assert.Equal("Default", created.Data!.CustomFieldValues["Ref"].GetString());
        Assert.Equal("{\"Ref\":null}", (await DbContextForAssert.Cards.SingleAsync(card => card.Title == "Existing")).CustomFieldValuesJson);
    }

    [Fact]
    public async Task Update_DefaultAndPresentation_ShouldPreserveValuesAndContentTimestamp()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        var updated = Points with { Name = "Estimate", DefaultValue = 2, Emoji = "🎯", ShowOnCard = true };
        var result = await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), Input(updated), ActorUserId);

        Assert.True(result.Success);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CustomFieldValuesJson, stored.CustomFieldValuesJson);
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_KindOrConstraint_ShouldPreviewAndReplaceInvalidValues(bool changeKind)
    {
        CustomFieldDefinition original = new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true };
        var card = await ArrangeAsync(original, "{\"Ref\":\"too long\"}");
        CustomFieldDefinition changed = changeKind
            ? new IntegerCustomFieldDefinition { Code = "Ref", Name = "Reference", DefaultValue = 2 }
            : new TextCustomFieldDefinition { Code = "Ref", Name = "Reference", Nullable = true, MaxLength = 2 };
        var input = Input(changed);
        var preview = await Service.PreviewAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Ref"), new(input), ActorUserId);
        Assert.Equal(new CustomFieldPreviewDto(1, 0, 1), preview.Data);
        Assert.Equal(card.CustomFieldValuesJson, (await DbContextForAssert.Cards.AsNoTracking().SingleAsync()).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.AsNoTracking().ToListAsync());

        var result = await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Ref"), input, ActorUserId);

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(changeKind ? "{\"Ref\":2}" : "{\"Ref\":null}", stored.CustomFieldValuesJson);
        Assert.Contains("Reference: too long", (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Theory]
    [InlineData("null", 0)]
    [InlineData("0", 1)]
    public async Task RemoveApplicability_ShouldRetainUnusedDefinitionAndRecordOnlyPopulatedLosses(string value, int lossCount)
    {
        var card = await ArrangeAsync(Points with { Nullable = true }, "{\"Points\":" + value + "}");
        var input = Input(Points with { Nullable = true }, new SelectedCardTypes([]));
        var preview = await Service.PreviewAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), new(input), ActorUserId);
        var result = await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), input, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(new CustomFieldPreviewDto(lossCount, lossCount, 0), preview.Data);
        Assert.Empty(Assert.IsType<SelectedCardTypes>(Assert.Single(result.Data!).AppliesTo).CardTypeIds);
        Assert.Equal("{}", (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
        Assert.Equal(lossCount, await DbContextForAssert.CardComments.CountAsync());
    }

    [Fact]
    public async Task MakeRequired_ShouldFillExistingNullWithoutLoss()
    {
        var card = await ArrangeAsync(Points with { Nullable = true }, "{\"Points\":null}");
        var saved = await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), Input(Points with { DefaultValue = 9 }), ActorUserId);

        Assert.True(saved.Success);
        Assert.Equal("{\"Points\":9}", (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("foreign-type")]
    [InlineData("duplicate-type")]
    [InlineData("immutable")]
    public async Task Writes_ShouldValidateBoardWideIdentityAndApplicability(string invalid)
    {
        var card = await ArrangeAsync(Points, "{\"Points\":0}");
        var other = CreateBoard("Other").AddColumn("Todo").Build();
        var foreign = await DbContextForArrange.CardTypes.SingleAsync(type => type.BoardId == other.BoardId);
        var definition = Points with { Code = "Other", Name = "Other" };
        CustomFieldApplicability applies = new AllCardTypes();
        if (invalid == "code") definition = definition with { Code = "points" };
        if (invalid == "name") definition = definition with { Name = "POINTS" };
        if (invalid == "foreign-type") applies = new SelectedCardTypes([foreign.Id]);
        if (invalid == "duplicate-type") applies = new SelectedCardTypes([card.CardTypeId, card.CardTypeId]);

        var result = invalid == "immutable"
            ? await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), Input(definition), ActorUserId)
            : await Service.CreateAsync(card.BoardId, Input(definition, applies), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Single((await DbContextForAssert.Boards.Include(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync(board => board.Id == card.BoardId)).CustomFields.ToCustomFieldDtos());
        Assert.Equal(card.CustomFieldValuesJson, (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Rename_ToExistingName_ShouldAttachErrorToEditedDefinition(bool editFirst, bool preview)
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        var other = Points with { Code = "Other", Name = "Other" };
        Assert.True((await Service.CreateAsync(card.BoardId, Input(other), ActorUserId)).Success);
        var definition = editFirst ? Points with { Name = " other " } : other with { Name = " points " };
        var input = Input(definition);

        var result = preview
            ? (await Service.PreviewAsync(card.BoardId, await FieldIdAsync(card.BoardId, definition.Code), new(input), ActorUserId)).ValidationErrors
            : (await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, definition.Code), input, ActorUserId)).ValidationErrors;

        Assert.NotNull(result);
        Assert.Equal("definition.name", Assert.Single(result).Key);
        Assert.Contains("Name already used", result["definition.name"]);
        Assert.Equal(new[] { "Points", "Other" }, (await Service.GetAsync(card.BoardId, ActorUserId)).Data!
            .Select(field => field.Definition.Name));
    }

    [Fact]
    public async Task Limit_ShouldCountUnusedDefinitionsAcrossBoard()
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        for (var index = 0; index < 10; index++)
        {
            var created = await Service.CreateAsync(board.BoardId, Input(Points with { Code = "F" + index, Name = "Field " + index }, new SelectedCardTypes([])), ActorUserId);
            Assert.True(created.Success);
        }

        var result = await Service.CreateAsync(board.BoardId, Input(Points), ActorUserId);

        Assert.False(result.Success);
        Assert.Contains("10 fields maximum", result.ValidationErrors!["customFields"]);
    }

    [Fact]
    public async Task Reorder_ShouldPreserveValuesAndRejectIncompleteOrder()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        Assert.True((await Service.CreateAsync(card.BoardId, Input(Points with { Code = "Other", Name = "Other" }), ActorUserId)).Success);
        var before = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        var beforeFields = (await Service.GetAsync(card.BoardId, ActorUserId)).Data!;
        var assignmentIds = await DbContextForAssert.CustomFieldCardTypes.Select(assignment => assignment.Id).OrderBy(id => id).ToArrayAsync();
        var requestedIds = beforeFields.Reverse().Select(field => field.Id).ToArray();
        var ordered = await Service.ReorderAsync(card.BoardId, new(requestedIds), ActorUserId);
        var invalid = await Service.ReorderAsync(card.BoardId, new([beforeFields[0].Id, beforeFields[0].Id]), ActorUserId);

        Assert.True(ordered.Success);
        Assert.NotNull(ordered.Data);
        Assert.Equal(new[] { "Other", "Points" }, ordered.Data.Select(field => field.Definition.Code));
        Assert.Equal(requestedIds, ordered.Data.Select(field => field.Id));
        Assert.Equal(beforeFields.Reverse().Select(field => field.Definition), ordered.Data.Select(field => field.Definition));
        Assert.Equal(assignmentIds, await DbContextForAssert.CustomFieldCardTypes.Select(assignment => assignment.Id).OrderBy(id => id).ToArrayAsync());
        Assert.False(invalid.Success);
        var after = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(before.CustomFieldValuesJson, after.CustomFieldValuesJson);
        Assert.Equal(before.CardUpdatedUtc, after.CardUpdatedUtc);
    }

    [Fact]
    public async Task Delete_WhenCommentWriteFails_ShouldRollbackCatalogueValuesAndHistory()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectLoss BEFORE INSERT ON "CardComments"
            BEGIN SELECT RAISE(ABORT, 'Injected comment failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(async () => await Service.DeleteAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), ActorUserId));

        Assert.Single((await DbContextForAssert.Boards.Include(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync()).CustomFields.ToCustomFieldDtos());
        Assert.Equal(card.CustomFieldValuesJson, (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task DuplicateType_ShouldShareSelectedFieldsAndDeletionShouldRetainUnusedDefinitions()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        var service = ResolveService<CardTypeService>();
        var duplicate = await service.CreateCardTypeAsync(card.BoardId, new("Copy", CopyFieldAssignmentsFromCardTypeId: card.CardTypeId), ActorUserId);
        Assert.Equal("Points", Assert.Single(duplicate.Data!.CustomFields).Code);
        var catalogue = (await Service.GetAsync(card.BoardId, ActorUserId)).Data!;
        Assert.Equal(new[] { card.CardTypeId, duplicate.Data.Id }, Assert.IsType<SelectedCardTypes>(catalogue[0].AppliesTo).CardTypeIds);
        Assert.True((await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), Input(Points, new SelectedCardTypes([duplicate.Data.Id])), ActorUserId)).Success);

        var deleted = await service.DeleteCardTypeAsync(card.BoardId, duplicate.Data.Id, ActorUserId);

        Assert.True(deleted.Success);
        Assert.Empty(Assert.IsType<SelectedCardTypes>(Assert.Single((await Service.GetAsync(card.BoardId, ActorUserId)).Data!).AppliesTo).CardTypeIds);
    }

    [Fact]
    public async Task DuplicateType_WhenCatalogueWriteFails_ShouldRollbackTheNewType()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectCatalogue BEFORE INSERT ON "CustomFieldCardTypes"
            BEGIN SELECT RAISE(ABORT, 'Injected catalogue failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => ResolveService<CardTypeService>().CreateCardTypeAsync(
            card.BoardId, new("Copy", CopyFieldAssignmentsFromCardTypeId: card.CardTypeId), ActorUserId));

        Assert.Single(await DbContextForAssert.CardTypes.ToListAsync());
        var catalogue = (await DbContextForAssert.Boards.Include(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync()).CustomFields.ToCustomFieldDtos();
        Assert.Equal(new[] { card.CardTypeId }, Assert.IsType<SelectedCardTypes>(catalogue[0].AppliesTo).CardTypeIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpandApplicability_ShouldInitialiseOnlyNewlyIncludedCards(bool nullable)
    {
        var field = Points with { Nullable = nullable, DefaultValue = 3 };
        var card = await ArrangeAsync(field, "{\"Points\":7}");
        var other = await ResolveService<CardTypeService>().CreateCardTypeAsync(card.BoardId, new("Other"), ActorUserId);
        var created = await ResolveService<CardService>().CreateCardAsync(card.BoardId, new(null, "Other card", "", [], other.Data!.Id), ActorUserId);

        var updated = await Service.UpdateAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), Input(field), ActorUserId);

        Assert.True(updated.Success);
        var values = (await DbContextForAssert.Cards.OrderBy(item => item.BoardCardId).ToListAsync())
            .Select(item => CustomFieldJsonCodec.DeserialiseValues(item.CustomFieldValuesJson)["Points"]).ToArray();
        Assert.Equal(7, values[0].GetInt32());
        if (nullable) { Assert.Equal(JsonValueKind.Null, values[1].ValueKind); }
        else { Assert.Equal(3, values[1].GetInt32()); }
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.True(created.Success);
    }

    [Fact]
    public async Task Contributor_ShouldReadButNotWriteOrPreview()
    {
        var card = await ArrangeAsync(Points, "{\"Points\":0}");
        var user = new EntityUser { UserName = "Reader", Email = "reader@localhost", NormalisedEmail = "reader@localhost", PasswordHash = "test", IsActive = true };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.BoardMembers.Add(new() { BoardId = card.BoardId, UserId = user.Id, Role = BoardMemberRole.Contributor });
        await DbContextForArrange.SaveChangesAsync();

        Assert.True((await Service.GetAsync(card.BoardId, user.Id)).Success);
        Assert.Equal(403, (await Service.DeleteAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), user.Id)).StatusCode);
        Assert.Equal(403, (await Service.PreviewAsync(card.BoardId, await FieldIdAsync(card.BoardId, "Points"), new(null), user.Id)).StatusCode);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("preview")]
    [InlineData("reorder")]
    public async Task FieldIds_FromAnotherBoard_ShouldNotTargetTheLocalFieldWithTheSameCode(string operation)
    {
        var card = await ArrangeAsync(Points, "{\"Points\":7}");
        var other = CreateBoard("Other").AddColumn("Todo").Build();
        var created = await Service.CreateAsync(other.BoardId, Input(Points), ActorUserId);
        var foreignId = Assert.Single(created.Data!).Id;

        var status = operation switch
        {
            "update" => (await Service.UpdateAsync(card.BoardId, foreignId, Input(Points with { Name = "Changed" }), ActorUserId)).StatusCode,
            "delete" => (await Service.DeleteAsync(card.BoardId, foreignId, ActorUserId)).StatusCode,
            "preview" => (await Service.PreviewAsync(card.BoardId, foreignId, new(null), ActorUserId)).StatusCode,
            _ => (await Service.ReorderAsync(card.BoardId, new([foreignId]), ActorUserId)).StatusCode
        };

        Assert.Equal(operation == "reorder" ? 400 : 404, status);
        var catalogue = (await Service.GetAsync(card.BoardId, ActorUserId)).Data!;
        Assert.Equal("Points", Assert.Single(catalogue).Definition.Name);
        Assert.Equal(card.CustomFieldValuesJson, (await DbContextForAssert.Cards.SingleAsync()).CustomFieldValuesJson);
    }

    private Task<int> FieldIdAsync(int boardId, string code) =>
        DbContextForAssert.CustomFields.Where(field => field.BoardId == boardId && field.Code == code).Select(field => field.Id).SingleAsync();

    private async Task<EntityBoardCard> ArrangeAsync(CustomFieldDefinition definition, string values)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Description").Build();
        var card = board.GetCard("Card");
        SetFields(card.CardType, [definition]);
        card.CustomFieldValuesJson = values;
        await DbContextForArrange.SaveChangesAsync();
        return card;
    }
}
