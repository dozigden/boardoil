using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Services.Board.Import;

namespace BoardOil.Services.Board;

public sealed class BoardPackageImportService(
    BoardPackageImportReader importReader,
    BoardPackageImportPlanner importPlanner,
    BoardPackageImportWriter importWriter) : IBoardPackageImportService
{
    public async Task<ApiResult<BoardDto>> ImportBoardPackageAsync(ImportBoardPackageRequest request, int actorUserId)
    {
        if (request.PackageContent is null || request.PackageContent.Length == 0)
        {
            return ApiErrors.ValidationFailed([new ValidationError("file", "Board package ZIP file is required.")]);
        }

        var readPackageResult = importReader.TryReadBoardPackage(request.PackageContent);
        if (readPackageResult.Error is not null)
        {
            return readPackageResult.Error;
        }

        var boardName = BoardPackageImportNormalisation.ResolveImportedBoardName(request.Name, readPackageResult.BoardPayload!.Name);
        var boardDescription = BoardPackageImportNormalisation.ResolveImportedBoardDescription(readPackageResult.BoardPayload.Description);
        var planResult = importPlanner.BuildBoardPackageImportPlan(
            boardName,
            boardDescription,
            readPackageResult.BoardPayload.SlickCohesionModeEnabled,
            readPackageResult.BoardPayload,
            readPackageResult.ArchivePayload);
        if (planResult.Error is not null)
        {
            return planResult.Error;
        }

        return await importWriter.PersistBoardPackageImportAsync(planResult.Plan!, actorUserId);
    }
}
