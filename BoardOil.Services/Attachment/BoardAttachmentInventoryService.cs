using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Attachment;

public sealed class BoardAttachmentInventoryService(
    IAttachmentRepository attachments,
    IBoardAuthorisationService authorisation,
    IDbContextScopeFactory scopes) : IBoardAttachmentInventoryService
{
    public async Task<ApiResult<BoardAttachmentInventoryDto>> ListAsync(
        int boardId, int actorUserId, BoardAttachmentInventoryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly();
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings))
        {
            return ApiErrors.Forbidden("Owner permission required to view board attachments.");
        }

        query ??= new BoardAttachmentInventoryQuery();
        var errors = ValidateQuery(query);
        if (errors.Count > 0) { return ApiErrors.BadRequest("Invalid attachment inventory query.", errors); }

        // This inventory is a snapshot of published uploads, not physical disk usage.
        // Archive metadata keeps the query independent of snapshot payload versions.
        var boardAttachments = attachments.Query()
            .Where(x => x.State == AttachmentState.Ready
                && ((x.Card != null && x.Card.BoardId == boardId)
                    || (x.ArchivedCard != null && x.ArchivedCard.BoardId == boardId)));
        var totals = await boardAttachments.GroupBy(x => 1)
            .Select(group => new { Count = group.Count(), Bytes = group.Sum(x => x.ByteLength) })
            .SingleOrDefaultAsync(cancellationToken);

        var matchingAttachments = boardAttachments;
        if (query.State == "live") { matchingAttachments = matchingAttachments.Where(x => x.CardId != null); }
        else if (query.State == "archived") { matchingAttachments = matchingAttachments.Where(x => x.ArchivedCardId != null); }
        var matchingCount = await matchingAttachments.CountAsync(cancellationToken);
        var items = await OrderAttachments(matchingAttachments, query)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(x => new BoardAttachmentInventoryItemDto(
                x.Id, x.OriginalFileName, x.ContentType, x.ByteLength, x.CreatedAtUtc,
                x.Card != null ? x.Card.BoardCardId : x.ArchivedCard!.OriginalCardId,
                x.Card != null ? x.Card.Title : x.ArchivedCard!.SearchTitle,
                x.ArchivedCardId != null))
            .ToListAsync(cancellationToken);

        return ApiResults.Ok(new BoardAttachmentInventoryDto(
            items, totals?.Count ?? 0, totals?.Bytes ?? 0, matchingCount, query.Offset, query.Limit));
    }

    private static IOrderedQueryable<EntityCardAttachment> OrderAttachments(
        IQueryable<EntityCardAttachment> attachments, BoardAttachmentInventoryQuery query)
    {
        IOrderedQueryable<EntityCardAttachment> ordered;
        switch (query.Sort)
        {
            case "name":
                ordered = query.Direction == "asc"
                    ? attachments.OrderBy(x => x.NormalisedFileName)
                    : attachments.OrderByDescending(x => x.NormalisedFileName);
                break;
            case "size":
                ordered = query.Direction == "asc"
                    ? attachments.OrderBy(x => x.ByteLength)
                    : attachments.OrderByDescending(x => x.ByteLength);
                break;
            default:
                ordered = query.Direction == "asc"
                    ? attachments.OrderBy(x => x.CreatedAtUtc)
                    : attachments.OrderByDescending(x => x.CreatedAtUtc);
                break;
        }
        // IDs make equal names, dates and sizes stable across page boundaries.
        return query.Direction == "asc" ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
    }

    private static List<ValidationError> ValidateQuery(BoardAttachmentInventoryQuery query)
    {
        var errors = new List<ValidationError>();
        if (query.Offset < 0) { errors.Add(new ValidationError("offset", "Offset must be 0 or greater.")); }
        if (query.Limit is < 1 or > 200) { errors.Add(new ValidationError("limit", "Limit must be between 1 and 200.")); }
        if (query.Sort is not ("name" or "date" or "size")) { errors.Add(new ValidationError("sort", "Sort must be name, date or size.")); }
        if (query.Direction is not ("asc" or "desc")) { errors.Add(new ValidationError("direction", "Direction must be asc or desc.")); }
        if (query.State is not ("live" or "archived" or "both")) { errors.Add(new ValidationError("state", "State must be live, archived or both.")); }
        return errors;
    }
}
