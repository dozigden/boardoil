using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Services.Style;

namespace BoardOil.Services.Card;

public sealed class CreateCardService(
    ICardRepository cardRepository,
    IBoardCardIdAllocator boardCardIdAllocator,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    ICardValidator validator,
    CreateCardPlanner planner,
    CardInsertionOrderPlanner insertionOrderPlanner,
    IBoardEvents boardEvents,
    IBoardStyleDefaultService styleDefaultService,
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
        var draft = planner.BuildDraft(request, targetColumn, selectedCardType);
        var tags = await CardTagMutation.ResolveTagsAsync(
            boardId,
            request.TagNames ?? Array.Empty<string>(),
            _tagRepository,
            styleDefaultService);
        var selectedSlick = await CardSlickMutation.ResolveSlickAsync(boardId, request.SlickName, slickRepository, styleDefaultService);

        var card = new EntityBoardCard
        {
            BoardId = boardId,
            BoardColumnId = draft.TargetColumnId,
            CardTypeId = draft.CardTypeId,
            CardType = selectedCardType,
            AssignedUserId = draft.AssignedUserId,
            Slick = selectedSlick,
            Title = draft.Title,
            Description = draft.Description,
            ExternalUrl = draft.ExternalUrl,
            SortKey = string.Empty,
        };
        CardTagMutation.ReplaceTags(card, tags);

        var orderPlan = insertionOrderPlanner.CreateLeadingPlan(card, cards);
        if (orderPlan.Error is not null)
        {
            return orderPlan.Error;
        }

        foreach (var assignment in orderPlan.Assignments)
        {
            assignment.Card.SortKey = assignment.SortKey;
        }

        await scope.Transaction(async (transactionScope, transaction) =>
        {
            card.BoardCardId = await boardCardIdAllocator.AllocateNextAsync(boardId);
            cardRepository.Add(card);
            await transactionScope.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        var created = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
        await boardEvents.CardCreatedAsync(boardId, created);
        if (orderPlan.Renormalised)
        {
            await boardEvents.ResyncRequestedAsync(boardId);
        }

        return ApiResults.Created(created);
    }}
