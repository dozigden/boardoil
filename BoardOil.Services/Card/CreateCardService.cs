using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Image;
using BoardOil.Persistence.Abstractions.Tag;

namespace BoardOil.Services.Card;

public sealed class CreateCardService(
    ICardRepository cardRepository,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    ICardValidator validator,
    CreateCardPlanner planner,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory)
{
    private readonly ITagRepository _tagRepository = tagRepository;

    public async Task<ApiResult<CardDto>> ExecuteAsync(int boardId, CreateCardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardCreate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var validationErrors = await validator.ValidateCreateAsync(boardId, request);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var targetColumn = request.BoardColumnId is int requestedBoardColumnId
            ? columnRepository.Get(requestedBoardColumnId)
            : (await columnRepository.GetColumnsInBoardOrderedAsync(boardId)).FirstOrDefault();
        if (targetColumn is null)
        {
            var message = request.BoardColumnId is null
                ? "Board does not contain any columns."
                : "Column does not exist in board.";
            return ApiErrors.ValidationFailed([new ValidationError("boardColumnId", message)]);
        }

        if (targetColumn.BoardId != boardId)
        {
            return ApiErrors.ValidationFailed([new ValidationError("boardColumnId", "Column does not exist in board.")]);
        }

        EntityCardType? requestedCardType = null;
        if (request.CardTypeId is int requestedCardTypeId)
        {
            requestedCardType = await cardTypeRepository.GetByIdInBoardAsync(boardId, requestedCardTypeId);
        }

        var systemCardType = await cardTypeRepository.GetSystemByBoardIdAsync(boardId);

        var cardTypeSelection = planner.SelectCardType(
            request.CardTypeId,
            requestedCardType,
            systemCardType);
        if (cardTypeSelection.Error is not null)
        {
            return cardTypeSelection.Error;
        }
        var selectedCardType = cardTypeSelection.SelectedCardType!;

        var cards = await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id);
        var nextSortKey = cards.FirstOrDefault()?.SortKey;

        var draftResult = planner.BuildDraft(request, targetColumn, selectedCardType, nextSortKey);
        if (draftResult.Error is not null)
        {
            return draftResult.Error;
        }

        var draft = draftResult.Draft!.Value;
        var tags = await CardTagMutation.ResolveTagsAsync(boardId, request.TagNames ?? Array.Empty<string>(), _tagRepository);
        var card = new EntityBoardCard
        {
            BoardColumnId = draft.TargetColumnId,
            CardTypeId = draft.CardTypeId,
            CardType = selectedCardType,
            AssignedUserId = draft.AssignedUserId,
            Title = draft.Title,
            Description = draft.Description,
            SortKey = draft.SortKey,
        };
        CardTagMutation.ReplaceTags(card, tags);

        cardRepository.Add(card);
        await scope.SaveChangesAsync();

        var created = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
        await boardEvents.CardCreatedAsync(boardId, created);
        return ApiResults.Created(created);
    }}
