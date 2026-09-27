using System.Globalization;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Board;

namespace BoardOil.Services.Jobs;

public sealed class ResaveAllBoardsScheduledJobDefinition(
    IDbContextScopeFactory scopes, IBoardRepository boards) : IScheduledJobDefinition
{
    public const string ScheduleName = "resave-all-boards";
    public string Name => ScheduleName;
    public string DisplayName => "Resave all boards";
    public string SchedulerStateName => "resave-all-boards-checkpoint";

    public Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScheduledJobDefinitionConfiguration(true, Kind: ScheduledJobKind.Once));

    public async Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
        DateTime dueAtUtc, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        var ids = await boards.GetBoardIdsAsync(cancellationToken);
        return ids.Select(id => new ScheduledJobOccurrence(
            ResaveBoardJobHandler.JobType, JsonSerializer.Serialize(new ResaveBoardJobPayload(id)),
            id.ToString(CultureInfo.InvariantCulture))).ToArray();
    }
}
