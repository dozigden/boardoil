using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Services.Attachment;

namespace BoardOil.Services.Jobs;

public sealed record PurgeBoardJobPayload(int BoardId);

public sealed class PurgeBoardJobHandler(
    IBoardRepository boards,
    CardAttachmentService attachments,
    IDbContextScopeFactory scopes) : JobHandlerBase<PurgeBoardJobPayload>
{
    public const string JobType = "purge-board";

    public override string Type => JobType;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, PurgeBoardJobPayload payload, CancellationToken cancellationToken)
    {
        var boardId = payload.BoardId;
        if (boardId <= 0) { return JobHandlerResult.Failed("A positive board ID is required."); }

        IReadOnlyList<string>? attachmentKeys = null;
        using (var scope = scopes.Create())
        {
            await scope.Transaction(async (transactionScope, transaction) =>
            {
                var board = await boards.GetIncludingDeletedAsync(boardId, cancellationToken);
                if (board is null || board.DeletedAtUtc is null)
                {
                    return;
                }

                // Save the detachments before the board delete cascades to their cards.
                attachmentKeys = await attachments.DeleteForBoardAsync(boardId, cancellationToken);
                await transactionScope.SaveChangesAsync(cancellationToken);
                boards.Remove(board);
                await transactionScope.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync();
            });
        }

        if (attachmentKeys is null)
        {
            return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new { boardId, skipped = true }));
        }

        var attachmentsDeleted = await attachments.DeleteFilesAsync(attachmentKeys, cancellationToken);
        var remaining = attachmentKeys.Count - attachmentsDeleted;
        if (remaining > 0)
        {
            return JobHandlerResult.Failed(
                $"Purged board {boardId}. {remaining} attachment(s) remain for startup cleanup.");
        }

        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new { boardId, skipped = false, attachmentsDeleted }));
    }
}
