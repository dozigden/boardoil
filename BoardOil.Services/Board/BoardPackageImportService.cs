using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Services.Board.Import;
using BoardOil.Services.Attachment;

namespace BoardOil.Services.Board;

public sealed class BoardPackageImportService(
    BoardPackageImportReader importReader,
    BoardPackageImportPlanner importPlanner,
    BoardPackageImportWriter importWriter,
    BoardPackageAttachmentImporter attachmentImporter,
    BoardPackageStorageService packages) : IBoardPackageImportService
{
    public async Task<ApiResult<BoardDto>> ImportBoardPackageAsync(ImportBoardPackageRequest request, int actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.PackageContent is null || !request.PackageContent.CanRead)
        {
            return ApiErrors.ValidationFailed([new ValidationError("file", "Board package ZIP file is required.")]);
        }

        using var package = await packages.CreateAsync(cancellationToken);
        try
        {
            await request.PackageContent.CopyToAsync(package, cancellationToken);
            package.Position = 0;
            var readPackageResult = importReader.TryReadBoardPackage(package);
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
                readPackageResult.ArchivePayload,
                readPackageResult.SchemaVersion!.Value);
            if (planResult.Error is not null)
            {
                return planResult.Error;
            }

            package.Position = 0;
            var attachments = await attachmentImporter.PrepareAsync(package, readPackageResult.Attachments, planResult.Plan!, cancellationToken);
            return await importWriter.PersistBoardPackageImportAsync(planResult.Plan!, actorUserId, attachments, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or AttachmentSizeException or ArgumentException)
        {
            return ApiErrors.ValidationFailed([new ValidationError("file", exception.Message)]);
        }
    }
}
