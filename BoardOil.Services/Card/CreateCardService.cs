using System.Text.Json;
using BoardOil.Services.Attachment;
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
using BoardOil.Services.CardField;
using Microsoft.EntityFrameworkCore;

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
    IDbContextScopeFactory scopeFactory,
    CardAttachmentService attachments)
{
    private readonly ITagRepository _tagRepository = tagRepository;

    public async Task<ApiResult<CardDto>> ExecuteAsync(int boardId, CreateCardRequest request, int actorUserId,
        int? duplicateFromCardId = null, CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, JsonElement>? fields = null)
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
        var createdAtUtc = DateTime.UtcNow;

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
            CardCreatedUtc = createdAtUtc,
            CardUpdatedUtc = createdAtUtc,
        };
        CardChecklistCounter.Refresh(card);
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

        CardAttachmentCopy? attachmentCopy = null;
        if (duplicateFromCardId.HasValue)
        {
            var fieldPlan = await PlanFieldsAsync(boardId, selectedCardType.Id, duplicateFromCardId,
                fields, cancellationToken);
            if (!fieldPlan.Success)
            {
                return new(false, null, fieldPlan.StatusCode, fieldPlan.Message, fieldPlan.ValidationErrors);
            }
            // All draft validation has passed; only now create independent attachment files.
            attachmentCopy = await attachments.PrepareCopyAsync(boardId, duplicateFromCardId.Value, actorUserId, cancellationToken);
        }

        ApiResult<CardDto>? fieldError = null;
        EntityCardComment? lossComment = null;
        await scope.Transaction(async (transactionScope, transaction) =>
        {
            var fieldPlan = await PlanFieldsAsync(boardId, selectedCardType.Id, duplicateFromCardId,
                fields, cancellationToken);
            if (!fieldPlan.Success)
            {
                fieldError = new(false, null, fieldPlan.StatusCode, fieldPlan.Message, fieldPlan.ValidationErrors);
                return;
            }
            card.FieldValuesJson = fieldPlan.Data!.ValuesJson;
            lossComment = CardFieldLossComment.Add(card, fieldPlan.Data.LostValues);
            CardChecklistCounter.Refresh(card);
            card.BoardCardId = await boardCardIdAllocator.AllocateNextAsync(boardId);
            if (attachmentCopy is not null) { await attachments.AttachCopyAsync(attachmentCopy, card, actorUserId); }
            cardRepository.Add(card);
            cancellationToken.ThrowIfCancellationRequested();
            await transactionScope.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync();
        });
        if (fieldError is not null)
        {
            return fieldError;
        }

        var created = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
        await boardEvents.CardCreatedAsync(boardId, created);
        if (lossComment is not null)
        {
            await boardEvents.CommentCreatedAsync(boardId, lossComment.ToCardCommentDto(card.BoardCardId));
        }
        if (orderPlan.Renormalised)
        {
            await boardEvents.ResyncRequestedAsync(boardId);
        }

        return ApiResults.Created(created);
    }

    private async Task<ApiResult<CardCreationFieldPlan>> PlanFieldsAsync(int boardId, int targetTypeId, int? sourceCardId,
        IReadOnlyDictionary<string, JsonElement>? fields, CancellationToken cancellationToken)
    {
        // Read fresh JSON so the publication check does not reuse tracked definitions from before file preparation.
        var definitionsJson = await cardTypeRepository.Query()
            .Where(type => type.BoardId == boardId && type.Id == targetTypeId)
            .Select(type => type.FieldDefinitionsJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (definitionsJson is null)
        {
            return ApiErrors.ValidationFailed([new ValidationError("cardTypeId", "Card type does not exist in board.")]);
        }
        var definitions = CardFieldJsonCodec.DeserialiseDefinitions(definitionsJson);
        IReadOnlyList<LostCardFieldValue> losses = [];
        CardFieldRuleResult<IReadOnlyDictionary<string, JsonElement>> initialValues;
        if (sourceCardId is int id)
        {
            var source = await cardRepository.Query()
                .Where(item => item.BoardId == boardId && item.BoardCardId == id)
                .Select(item => new { item.CardTypeId, item.FieldValuesJson, item.CardType.FieldDefinitionsJson })
                .SingleOrDefaultAsync(cancellationToken);
            if (source is null) { throw new AttachmentOwnerChangedException(); }
            var sourceDefinitions = CardFieldJsonCodec.DeserialiseDefinitions(source.FieldDefinitionsJson);
            initialValues = CardFieldRules.NormaliseValues(sourceDefinitions,
                fields ?? CardFieldJsonCodec.DeserialiseValues(source.FieldValuesJson));
            if (initialValues.IsValid && source.CardTypeId != targetTypeId)
            {
                var conversion = CardFieldConverter.ConvertBetweenTypes(sourceDefinitions, initialValues.Value!, definitions);
                initialValues = new(conversion.Value?.Values, conversion.Errors);
                losses = conversion.Value?.LostValues ?? [];
            }
        }
        else
        {
            initialValues = CardFieldRules.CreateValues(definitions, fields);
        }
        if (!initialValues.IsValid)
        {
            return ApiErrors.ValidationFailed(initialValues.Errors);
        }
        return new CardCreationFieldPlan(CardFieldJsonCodec.SerialiseValues(initialValues.Value!), losses);
    }

    private sealed record CardCreationFieldPlan(string ValuesJson, IReadOnlyList<LostCardFieldValue> LostValues);
}
