using System.Data;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Board.Import;

namespace BoardOil.Services.Board;

public sealed class BoardCloneService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardTypeRepository cardTypeRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IBoardAuthorisationService boardAuthorisationService,
    BoardPackageImportPlanner importPlanner,
    BoardPackageImportWriter importWriter,
    IDbContextScopeFactory scopeFactory) : IBoardCloneService
{
    public async Task<ApiResult<BoardDto>> CloneBoardAsync(
        int sourceBoardId,
        CloneBoardRequest request,
        int actorUserId)
    {
        BoardPackageBoardDto sourceConfiguration;
        using (var scope = scopeFactory.CreateReadOnlyWithTransaction(IsolationLevel.Serializable))
        {
            var sourceBoard = boardRepository.Get(sourceBoardId);
            if (sourceBoard is null)
            {
                return ApiErrors.NotFound("Board not found.");
            }

            var hasPermission = await boardAuthorisationService.HasPermissionAsync(
                sourceBoardId,
                actorUserId,
                BoardPermission.BoardAccess);
            if (!hasPermission)
            {
                return ApiErrors.Forbidden("You do not have access to this board.");
            }

            var columns = await columnRepository.GetColumnsInBoardOrderedAsync(sourceBoardId);
            var cardTypes = await cardTypeRepository.GetAllForBoardAsync(sourceBoardId);
            var tags = await tagRepository.GetAllForBoardAsync(sourceBoardId);
            var slicks = await slickRepository.GetAllForBoardAsync(sourceBoardId);

            sourceConfiguration = new BoardPackageBoardDto(
                sourceBoard.Name,
                sourceBoard.Description,
                cardTypes
                    .Select(x => new BoardPackageCardTypeDto(
                        x.Name,
                        x.Emoji,
                        x.IsSystem,
                        x.StyleName,
                        x.StylePropertiesJson))
                    .ToList(),
                tags
                    .Select(x => new BoardPackageTagDto(
                        x.Name,
                        x.StyleName,
                        x.StylePropertiesJson,
                        x.Emoji))
                    .ToList(),
                columns
                    .Select(x => new BoardPackageColumnDto(x.Title, []))
                    .ToList(),
                slicks
                    .Select(x => new BoardPackageSlickDto(
                        x.Name,
                        x.StyleName,
                        x.StylePropertiesJson))
                    .ToList(),
                sourceBoard.SlickCohesionModeEnabled);
        }

        var targetName = request.Name?.Trim() ?? string.Empty;
        var targetDescription = sourceConfiguration.Description?.Trim() ?? string.Empty;
        var planResult = importPlanner.BuildBoardPackageImportPlan(
            targetName,
            targetDescription,
            sourceConfiguration.SlickCohesionModeEnabled,
            sourceConfiguration,
            archivePayload: null,
            BoardPackageContract.CurrentSchemaVersion);
        if (planResult.Error is not null)
        {
            return planResult.Error;
        }

        return await importWriter.PersistBoardPackageImportAsync(planResult.Plan!, actorUserId);
    }
}
