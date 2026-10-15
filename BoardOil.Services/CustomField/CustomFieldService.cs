using System.Text.Json;
using BoardOil.Services.Card;
using BoardOil.Abstractions.CustomField;
using BoardOil.Abstractions;
using System.Data;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CustomField;

public sealed class CustomFieldService(
    IBoardRepository boards,
    ICardTypeRepository types,
    ICardRepository cards,
    IBoardAuthorisationService authorisation,
    IBoardEvents events,
    IDbContextScopeFactory scopes) : ICustomFieldService
{
    public async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> GetAsync(int boardId, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (error is not null) { return error; }
        return ApiResults.Ok(boards.Get(boardId)!.CustomFields.ToCustomFieldDtos());
    }

    public async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> CreateAsync(int boardId, CustomFieldRequest input, int actorUserId)
    {
        var result = await SaveAsync(boardId, null, input, actorUserId);
        return result.Success ? ApiResults.Created(result.Data!) : result;
    }

    public Task<ApiResult<IReadOnlyList<CustomFieldDto>>> UpdateAsync(int boardId, int customFieldId, CustomFieldRequest input, int actorUserId) =>
        SaveAsync(boardId, customFieldId, input, actorUserId);

    public Task<ApiResult<IReadOnlyList<CustomFieldDto>>> DeleteAsync(int boardId, int customFieldId, int actorUserId) =>
        SaveAsync(boardId, customFieldId, null, actorUserId);

    private async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> SaveAsync(
        int boardId, int? customFieldId, CustomFieldRequest? input, int actorUserId)
    {
        using var scope = scopes.CreateWithTransaction(IsolationLevel.Serializable);
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (error is not null) { return error; }
        var board = boards.Get(boardId)!;
        var existing = board.CustomFields.SingleOrDefault(field => field.Id == customFieldId);
        if (customFieldId is not null && existing is null) { return ApiErrors.NotFound("Field not found."); }
        var original = board.CustomFields.ToCustomFieldDrafts();
        var next = await PrepareAsync(board, existing, input);
        if (!next.Success) { return new(false, null, next.StatusCode, next.Message, next.ValidationErrors); }
        var changes = await PlanAsync(boardId, original, next.Data!.Catalogue);
        if (!changes.IsValid) { return ApiErrors.ValidationFailed(changes.Errors); }
        var now = DateTime.UtcNow;
        foreach (var change in changes.Value!)
        {
            if (change.Changed)
            {
                change.Card.CustomFieldValuesJson = change.ValuesJson;
                change.Card.CardUpdatedUtc = now;
                CardChecklistCounter.Refresh(change.Card);
            }
            CardCommentService.AddCustomFieldLossComment(change.Card, change.LostValues,
                change.Card.CardType.Name, change.Card.CardType.Name, "field definition change");
        }
        if (existing is null)
        {
            var field = new EntityCustomField
            {
                Board = board,
                BoardId = board.Id,
                SortOrder = board.CustomFields.Select(item => item.SortOrder).DefaultIfEmpty(-1).Max() + 1
            };
            field.ApplyCustomFieldDraft(next.Data.Field!);
            board.CustomFields.Add(field);
        }
        else if (next.Data.Field is null)
        {
            board.CustomFields.Remove(existing);
        }
        else
        {
            existing.ApplyCustomFieldDraft(next.Data.Field);
        }
        await scope.SaveChangesAsync();
        await events.ResyncRequestedAsync(boardId);
        return ApiResults.Ok(board.CustomFields.ToCustomFieldDtos());
    }

    public async Task<ApiResult<CustomFieldPreviewDto>> PreviewAsync(
        int boardId, int customFieldId, CustomFieldPreviewRequest request, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (error is not null) { return error; }
        var board = boards.Get(boardId)!;
        var existing = board.CustomFields.SingleOrDefault(field => field.Id == customFieldId);
        if (existing is null) { return ApiErrors.NotFound("Field not found."); }
        var original = board.CustomFields.ToCustomFieldDrafts();
        var next = await PrepareAsync(board, existing, request.Field);
        if (!next.Success) { return new(false, null, next.StatusCode, next.Message, next.ValidationErrors); }
        var changes = await PlanAsync(boardId, original, next.Data!.Catalogue);
        if (!changes.IsValid) { return ApiErrors.ValidationFailed(changes.Errors); }
        var affected = changes.Value!.Where(change => change.LostValues.Count > 0).ToArray();
        var removed = 0;
        var replaced = 0;
        foreach (var change in affected)
        {
            var applies = next.Data.Field is not null && next.Data.Field.AppliesTo.AppliesToCardType(change.Card.CardTypeId);
            if (applies) { replaced += change.LostValues.Count; }
            else { removed += change.LostValues.Count; }
        }
        return new CustomFieldPreviewDto(affected.Length, removed, replaced);
    }

    public async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> ReorderAsync(
        int boardId, CustomFieldOrderRequest request, int actorUserId)
    {
        using var scope = scopes.CreateWithTransaction(IsolationLevel.Serializable);
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (error is not null) { return error; }
        var board = boards.Get(boardId)!;
        var byId = board.CustomFields.ToDictionary(field => field.Id);
        if (request.Ids is null || request.Ids.Count != byId.Count
            || request.Ids.Any(id => !byId.ContainsKey(id))
            || request.Ids.Distinct().Count() != byId.Count)
        {
            return ApiErrors.ValidationFailed([new("ids", "Supply every field ID exactly once")]);
        }
        for (var index = 0; index < request.Ids.Count; index++)
        {
            byId[request.Ids[index]].SortOrder = index;
        }
        await scope.SaveChangesAsync();
        await events.ResyncRequestedAsync(boardId);
        return ApiResults.Ok(board.CustomFields.ToCustomFieldDtos());
    }

    private sealed record PreparedCustomFieldEdit(
        CustomFieldDraft? Field, IReadOnlyList<CustomFieldDraft> Catalogue);

    private async Task<ApiResult<PreparedCustomFieldEdit>> PrepareAsync(
        EntityBoard board, EntityCustomField? existing, CustomFieldRequest? input)
    {
        var entities = board.CustomFields.OrderBy(field => field.SortOrder).ThenBy(field => field.Id).ToList();
        var next = entities.Select(field => field.ToCustomFieldDraft()).ToList();
        var index = existing is null ? next.Count : entities.IndexOf(existing);
        if (input is null)
        {
            next.RemoveAt(index);
        }
        else
        {
            var parsed = CustomFieldRequestParser.Parse(input);
            if (!parsed.IsValid) { return ApiErrors.ValidationFailed(parsed.Errors); }
            var field = parsed.Value!;
            if (existing is not null)
            {
                if (!string.Equals(field.Definition.Code?.Trim(), existing.Code, StringComparison.Ordinal))
                {
                    return ApiErrors.ValidationFailed([new("definition.code", "Code cannot be changed")]);
                }
                next[index] = field;
            }
            else
            {
                next.Add(field);
            }
        }
        var cardTypes = await types.GetAllForBoardAsync(board.Id);
        var validation = CustomFieldValidator.ValidateAndNormaliseDrafts(next, cardTypes.Select(type => type.Id).ToHashSet());
        if (!validation.IsValid)
        {
            return ApiErrors.ValidationFailed(validation.Errors.Select(error =>
                new ValidationError(InputValidationPath(error.Property), error.Message)).ToArray());
        }
        var catalogue = validation.Value!;
        return new PreparedCustomFieldEdit(input is null ? null : catalogue[index], catalogue);
    }

    private static string InputValidationPath(string path)
    {
        // Only one field is edited, but uniqueness validation can identify the other conflicting entry.
        var end = path.IndexOf("].", StringComparison.Ordinal);
        if (path.StartsWith("customFields[", StringComparison.Ordinal) && end >= 0)
        {
            return path[(end + 2)..];
        }
        return path;
    }

    private async Task<CustomFieldValidationResult<IReadOnlyList<CustomFieldChange>>> PlanAsync(
        int boardId, IReadOnlyList<CustomFieldDraft> source, IReadOnlyList<CustomFieldDraft> destination)
    {
        var changes = new List<CustomFieldChange>();
        foreach (var type in await types.GetAllForBoardAsync(boardId))
        {
            var before = source.GetApplicableCustomFieldDefinitions(type.Id);
            var after = destination.GetApplicableCustomFieldDefinitions(type.Id);
            if (before.SequenceEqual(after)) { continue; }
            var affectedCards = await cards.GetByBoardAndCardTypeAsync(boardId, type.Id);
            foreach (var card in affectedCards)
            {
                var originalValues = CustomFieldJsonCodec.DeserialiseValues(card.CustomFieldValuesJson);
                var conversion = CustomFieldConverter.ApplyDefinitionChanges(before, originalValues, after);
                if (!conversion.IsValid)
                {
                    return new(null, conversion.Errors.Select(error =>
                        new ValidationError($"cards[{card.BoardCardId}].{error.Property}", error.Message)).ToArray());
                }
                var converted = conversion.Value!;
                var valuesChanged = originalValues.Count != converted.Values.Count
                    || originalValues.Any(pair => !converted.Values.TryGetValue(pair.Key, out var value)
                        || !JsonElement.DeepEquals(pair.Value, value));
                changes.Add(new(card, CustomFieldJsonCodec.SerialiseValues(converted.Values), converted.LostValues, valuesChanged));
            }
        }
        return new(changes, []);
    }

    private sealed record CustomFieldChange(
        EntityBoardCard Card, string ValuesJson, IReadOnlyList<LostCustomFieldValue> LostValues, bool Changed);

    private async Task<ApiError?> CheckAccessAsync(int boardId, int actorUserId, BoardPermission permission)
    {
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, permission))
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }
        return boards.Get(boardId) is null ? ApiErrors.NotFound("Board not found.") : null;
    }
}
