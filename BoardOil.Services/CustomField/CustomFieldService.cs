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
        return ApiResults.Ok(CustomFieldCatalogue.Read(boards.Get(boardId)!.CustomFields));
    }

    public async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> CreateAsync(int boardId, CustomFieldInput input, int actorUserId)
    {
        var result = await SaveAsync(boardId, null, input, actorUserId);
        return result.Success ? ApiResults.Created(result.Data!) : result;
    }

    public Task<ApiResult<IReadOnlyList<CustomFieldDto>>> UpdateAsync(int boardId, string code, CustomFieldInput input, int actorUserId) =>
        SaveAsync(boardId, code, input, actorUserId);

    public Task<ApiResult<IReadOnlyList<CustomFieldDto>>> DeleteAsync(int boardId, string code, int actorUserId) =>
        SaveAsync(boardId, code, null, actorUserId);

    private async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> SaveAsync(
        int boardId, string? code, CustomFieldInput? input, int actorUserId)
    {
        using var scope = scopes.CreateWithTransaction(IsolationLevel.Serializable);
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (error is not null) { return error; }
        var board = boards.Get(boardId)!;
        var original = CustomFieldCatalogue.Read(board.CustomFields);
        var next = await PrepareAsync(boardId, original, code, input);
        if (!next.Success) { return next; }
        var changes = await PlanAsync(boardId, original, next.Data!);
        if (!changes.IsValid) { return ApiErrors.ValidationFailed(changes.Errors); }
        foreach (var group in changes.Value!.GroupBy(change => change.Card.CardType))
        {
            CustomFieldChanges.Apply(group.ToArray(), group.Key.Name, group.Key.Name);
        }
        CustomFieldCatalogue.Apply(board, next.Data!);
        await scope.SaveChangesAsync();
        await events.ResyncRequestedAsync(boardId);
        return next;
    }

    public async Task<ApiResult<CustomFieldPreviewDto>> PreviewAsync(
        int boardId, string code, CustomFieldPreviewRequest request, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        var error = await CheckAccessAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (error is not null) { return error; }
        var original = CustomFieldCatalogue.Read(boards.Get(boardId)!.CustomFields);
        var next = await PrepareAsync(boardId, original, code, request.Field);
        if (!next.Success) { return new(false, null, next.StatusCode, next.Message, next.ValidationErrors); }
        var changes = await PlanAsync(boardId, original, next.Data!);
        if (!changes.IsValid) { return ApiErrors.ValidationFailed(changes.Errors); }
        var affected = changes.Value!.Where(change => change.LostValues.Count > 0).ToArray();
        var removed = 0;
        var replaced = 0;
        foreach (var change in affected)
        {
            var applies = CustomFieldCatalogue.Resolve(next.Data!, change.Card.CardTypeId)
                .Any(field => string.Equals(field.Code, code, StringComparison.OrdinalIgnoreCase));
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
        var fields = CustomFieldCatalogue.Read(board.CustomFields);
        var byCode = fields.ToDictionary(field => field.Definition.Code, StringComparer.OrdinalIgnoreCase);
        if (request.Codes is null || request.Codes.Count != fields.Count
            || request.Codes.Any(code => code is null || !byCode.ContainsKey(code))
            || request.Codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Count)
        {
            return ApiErrors.ValidationFailed([new("codes", "Supply every field code exactly once")]);
        }
        var ordered = request.Codes.Select(code => byCode[code]).ToArray();
        CustomFieldCatalogue.Apply(board, ordered);
        await scope.SaveChangesAsync();
        await events.ResyncRequestedAsync(boardId);
        return ApiResults.Ok<IReadOnlyList<CustomFieldDto>>(ordered);
    }

    private async Task<ApiResult<IReadOnlyList<CustomFieldDto>>> PrepareAsync(
        int boardId, IReadOnlyList<CustomFieldDto> original, string? code, CustomFieldInput? input)
    {
        var next = original.ToList();
        var index = next.FindIndex(field => string.Equals(field.Definition.Code, code, StringComparison.OrdinalIgnoreCase));
        if (code is not null && index < 0) { return ApiErrors.NotFound("Field not found."); }
        if (input is null)
        {
            next.RemoveAt(index);
        }
        else
        {
            var parsed = CustomFieldCatalogue.Parse(input);
            if (!parsed.IsValid) { return ApiErrors.ValidationFailed(parsed.Errors); }
            var field = parsed.Value!;
            if (code is not null)
            {
                if (!string.Equals(field.Definition.Code?.Trim(), next[index].Definition.Code, StringComparison.Ordinal))
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
        var cardTypes = await types.GetAllForBoardAsync(boardId);
        var validation = CustomFieldCatalogue.Normalise(next, cardTypes.Select(type => type.Id).ToHashSet());
        if (!validation.IsValid)
        {
            return ApiErrors.ValidationFailed(validation.Errors.Select(error =>
                new ValidationError(InputValidationPath(error.Property), error.Message)).ToArray());
        }
        return ApiResults.Ok(validation.Value!);
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

    private async Task<CustomFieldRuleResult<IReadOnlyList<CustomFieldChange>>> PlanAsync(
        int boardId, IReadOnlyList<CustomFieldDto> source, IReadOnlyList<CustomFieldDto> destination)
    {
        var changes = new List<CustomFieldChange>();
        foreach (var type in await types.GetAllForBoardAsync(boardId))
        {
            var before = CustomFieldCatalogue.Resolve(source, type.Id);
            var after = CustomFieldCatalogue.Resolve(destination, type.Id);
            if (before.SequenceEqual(after)) { continue; }
            var affectedCards = await cards.GetByBoardAndCardTypeAsync(boardId, type.Id);
            var plan = CustomFieldChanges.Plan(affectedCards, before, after);
            if (!plan.IsValid) { return plan; }
            changes.AddRange(plan.Value!);
        }
        return new(changes, []);
    }

    private async Task<ApiError?> CheckAccessAsync(int boardId, int actorUserId, BoardPermission permission)
    {
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, permission))
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }
        return boards.Get(boardId) is null ? ApiErrors.NotFound("Board not found.") : null;
    }
}
