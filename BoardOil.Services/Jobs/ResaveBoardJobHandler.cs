using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Jobs;

namespace BoardOil.Services.Jobs;

public sealed record ResaveBoardJobPayload(int BoardId);

public sealed class ResaveBoardJobHandler(IBoardResaveService boards) : JobHandlerBase<ResaveBoardJobPayload>
{
    public const string JobType = "resave-board";
    public override string Type => JobType;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, ResaveBoardJobPayload payload, CancellationToken cancellationToken)
    {
        if (payload.BoardId <= 0) { return JobHandlerResult.Failed("A positive board ID is required."); }
        var result = await boards.ResaveAsync(payload.BoardId, cancellationToken);
        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new
        {
            boardId = result.BoardId,
            skipped = result.Skipped,
            cardsExamined = result.CardsExamined,
            cardsChanged = result.CardsChanged
        }));
    }
}
