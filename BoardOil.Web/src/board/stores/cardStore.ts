import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useSlickStore } from './slickStore';
import { useAttachmentStore } from './attachmentStore';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import type {
  BoardColumn,
  Card,
  CardEditModel,
  CardTransferPolicy
} from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

type CardMap = Record<number, Card>;
type CardIdsByColumnMap = Record<number, number[]>;

export const useCardStore = defineStore('card', () => {
  const cardsById = ref<CardMap>({});
  const cardIdsByColumnId = ref<CardIdsByColumnMap>({});
  const busy = ref(false);
  const activeBoardId = ref(0);
  const feedback = useUiFeedbackStore();
  const slickStore = useSlickStore();
  const attachmentStore = useAttachmentStore();
  const cardAttachmentThumbnailStore = useCardAttachmentThumbnailStore();
  const api = createBoardApi();

  function replaceBoardCards(boardId: number, columns: BoardColumn[]) {
    const nextCardsById: CardMap = {};
    const nextCardIdsByColumnId: CardIdsByColumnMap = {};

    for (const column of columns) {
      const sortedCards = [...column.cards].sort((left, right) => compareSortKey(left.sortKey, right.sortKey));
      nextCardIdsByColumnId[column.id] = sortedCards.map(card => card.id);
      for (const card of sortedCards) {
        nextCardsById[card.id] = cloneCard(card);
      }
    }

    activeBoardId.value = boardId;
    cardsById.value = nextCardsById;
    cardIdsByColumnId.value = nextCardIdsByColumnId;
  }

  function dispose() {
    activeBoardId.value = 0;
    cardsById.value = {};
    cardIdsByColumnId.value = {};
    busy.value = false;
  }

  async function createCard(
    model: CardEditModel,
    options?: { suppressValidationFeedback?: boolean; duplicateFromCardId?: number }
  ) {
    model.title = model.title.trim();
    if (!model.title) {
      return null;
    }

    const boardId = activeBoardId.value;
    const result = await runBusy(
      () => {
        if (options?.duplicateFromCardId !== undefined) {
          return api.duplicateCard(boardId, options.duplicateFromCardId, model);
        }
        return api.createCard(boardId, model);
      },
      {
        boardId,
        suppressError: options?.suppressValidationFeedback
          ? error => hasValidationErrors(error)
          : undefined
      }
    );
    if (!result.ok) {
      return result;
    }

    if (activeBoardId.value !== boardId) {
      return null;
    }

    if (options?.duplicateFromCardId !== undefined) {
      await applyCreatedCard(result.data);
    } else {
      upsertCard(result.data);
    }
    return result;
  }

  async function saveCard(cardId: number, model: CardEditModel) {
    const boardId = activeBoardId.value;
    const result = await runBusy(() => api.saveCard(boardId, cardId, model), { boardId });
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    upsertCard(result.data);
    return true;
  }

  async function deleteCard(cardId: number) {
    const boardId = activeBoardId.value;
    const result = await runBusy(() => api.deleteCard(boardId, cardId), { boardId });
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    removeCard(cardId);
    return true;
  }

  async function transferCard(
    cardId: number,
    destinationBoardId: number,
    destinationColumnId: number,
    transferPolicy: CardTransferPolicy
  ) {
    const sourceBoardId = activeBoardId.value;
    const result = await runBusy(
      () => api.transferCard(
        sourceBoardId,
        cardId,
        destinationBoardId,
        destinationColumnId,
        transferPolicy
      ),
      { boardId: sourceBoardId }
    );
    if (!result.ok) {
      return result;
    }

    if (activeBoardId.value === sourceBoardId) {
      removeCard(cardId);
    }

    return result;
  }

  async function deleteCards(cardIds: number[]) {
    const uniqueCardIds = [...new Set(cardIds)];
    const boardId = activeBoardId.value;
    const result = await runBusy(() => api.deleteCards(boardId, uniqueCardIds), { boardId });
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    removeCards(uniqueCardIds);

    return true;
  }

  async function archiveCard(cardId: number) {
    const boardId = activeBoardId.value;
    const result = await runBusy(() => api.archiveCard(boardId, cardId), { boardId });
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    removeCard(cardId);
    return true;
  }

  async function archiveCards(cardIds: number[]) {
    const uniqueCardIds = [...new Set(cardIds)];
    const boardId = activeBoardId.value;
    const result = await runBusy(() => api.archiveCards(boardId, uniqueCardIds), { boardId });
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    removeCards(uniqueCardIds);

    return true;
  }

  async function bulkMoveCards(
    cardIds: number[],
    targetColumnId: number,
    targetCardId: number | null
  ) {
    const uniqueCardIds = [...new Set(cardIds)];
    if (uniqueCardIds.length === 0) {
      return true;
    }

    return bulkEditCards(
      uniqueCardIds,
      {
        moveTargetColumnId: targetColumnId,
        moveTargetCardId: targetCardId
      }
    );
  }

  async function bulkEditCards(
    cardIds: number[],
    options: {
      moveTargetColumnId?: number | null;
      moveTargetCardId?: number | null;
      addTagNames?: string[];
      removeTagNames?: string[];
      slickName?: string | null;
    }
  ) {
    const uniqueCardIds = [...new Set(cardIds)];
    if (uniqueCardIds.length === 0) {
      return true;
    }

    const normalisedAddTagNames = normaliseTagNames(options.addTagNames ?? []);
    const normalisedRemoveTagNames = normaliseTagNames(options.removeTagNames ?? []);
    const hasSlickEditOperation = Object.prototype.hasOwnProperty.call(options, 'slickName');
    const slickPayload = hasSlickEditOperation
      ? { name: normaliseSlickName(options.slickName ?? null) }
      : undefined;

    let movePayload: { targetColumnId: number; positionAfterCardId: number | null } | null = null;
    if (typeof options.moveTargetColumnId === 'number') {
      const positionAfterCardId = resolvePositionAfterCardIdForBulkMove(
        cardsById.value,
        cardIdsByColumnId.value,
        uniqueCardIds,
        options.moveTargetColumnId,
        options.moveTargetCardId ?? null
      );
      if (positionAfterCardId === undefined) {
        return false;
      }

      movePayload = {
        targetColumnId: options.moveTargetColumnId,
        positionAfterCardId
      };
    }

    const boardId = activeBoardId.value;
    const result = await runBusy(
      () => api.editCards(boardId, {
        cardIds: uniqueCardIds,
        move: movePayload,
        addTagNames: normalisedAddTagNames,
        removeTagNames: normalisedRemoveTagNames,
        slick: slickPayload
      }),
      { boardId }
    );
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return false;
    }

    upsertCards(result.data);

    return true;
  }

  function normaliseTagNames(tagNames: string[]) {
    return [...new Set(
      tagNames
        .map(tagName => tagName.trim())
        .filter(tagName => tagName.length > 0)
    )];
  }

  function normaliseSlickName(slickName: string | null) {
    if (slickName === null) {
      return null;
    }

    const canonicalName = slickName.trim();
    if (canonicalName.length === 0) {
      return null;
    }

    return canonicalName;
  }

  async function moveCard(
    cardId: number,
    targetColumnId: number,
    targetCardId: number | null
  ) {
    const positionAfterCardId = resolvePositionAfterCardId(
      cardsById.value,
      cardIdsByColumnId.value,
      cardId,
      targetColumnId,
      targetCardId
    );
    if (positionAfterCardId === undefined) {
      return;
    }

    const boardId = activeBoardId.value;
    const result = await runBusy(
      () => api.moveCard(boardId, cardId, targetColumnId, positionAfterCardId),
      { boardId }
    );
    if (!result.ok) {
      return;
    }

    if (activeBoardId.value === boardId) {
      upsertCard(result.data);
    }
  }

  async function applyCreatedCard(card: Card) {
    const boardId = activeBoardId.value;
    upsertCard(card);
    await cardAttachmentThumbnailStore.refreshCards(boardId, [card.id]);
  }

  function upsertCard(card: Card) {
    upsertCards([card]);
  }

  function upsertCards(cards: Card[]) {
    if (cards.length === 0) {
      return;
    }

    const nextCardsById = { ...cardsById.value };
    const nextCardIdsByColumnId = unlinkCardsFromColumns(cardIdsByColumnId.value, new Set(cards.map(card => card.id)));
    const targetColumnIds = new Set<number>();

    for (const card of cards) {
      nextCardsById[card.id] = cloneCard(card);
      const targetCardIds = nextCardIdsByColumnId[card.boardColumnId] ?? [];
      targetCardIds.push(card.id);
      nextCardIdsByColumnId[card.boardColumnId] = targetCardIds;
      targetColumnIds.add(card.boardColumnId);
    }

    for (const columnId of targetColumnIds) {
      nextCardIdsByColumnId[columnId] = sortCardIds(nextCardIdsByColumnId[columnId], nextCardsById);
    }

    cardsById.value = nextCardsById;
    cardIdsByColumnId.value = nextCardIdsByColumnId;
    for (const card of cards) {
      if (card.slick) {
        slickStore.upsertSlick(activeBoardId.value, card.slick);
      }
    }
  }

  function removeCard(cardId: number) {
    removeCards([cardId]);
  }

  function removeCards(cardIds: number[]) {
    if (cardIds.length === 0) {
      return;
    }

    const nextCardsById = { ...cardsById.value };
    for (const cardId of cardIds) {
      delete nextCardsById[cardId];
    }

    const nextCardIdsByColumnId = unlinkCardsFromColumns(cardIdsByColumnId.value, new Set(cardIds));

    cardsById.value = nextCardsById;
    cardIdsByColumnId.value = nextCardIdsByColumnId;
    for (const cardId of cardIds) {
      cardAttachmentThumbnailStore.cardRemoved(activeBoardId.value, cardId);
      attachmentStore.cardRemoved(activeBoardId.value, cardId);
    }
  }

  function getCardById(cardId: number | null) {
    if (cardId === null) {
      return null;
    }

    return cardsById.value[cardId] ?? null;
  }

  function getCardsForColumn(columnId: number | null) {
    if (columnId === null) {
      return [];
    }

    const cardIds = cardIdsByColumnId.value[columnId] ?? [];
    const cards: Card[] = [];
    for (const cardId of cardIds) {
      const card = cardsById.value[cardId];
      if (card) {
        cards.push(card);
      }
    }

    return cards;
  }

  function removeTagFromCards(tagName: string) {
    const normalisedTagName = tagName.trim().toUpperCase();
    if (!normalisedTagName) {
      return;
    }

    const nextCardsById: CardMap = {};
    let hasChanges = false;

    for (const [key, card] of Object.entries(cardsById.value)) {
      const nextTagNames = card.tagNames.filter(existingTagName => existingTagName.trim().toUpperCase() !== normalisedTagName);
      const nextTags = card.tags.filter(existingTag => existingTag.name.trim().toUpperCase() !== normalisedTagName);
      if (nextTagNames.length !== card.tagNames.length) {
        hasChanges = true;
        nextCardsById[Number(key)] = {
          ...card,
          tags: nextTags,
          tagNames: nextTagNames
        };
        continue;
      }

      nextCardsById[Number(key)] = card;
    }

    if (hasChanges) {
      cardsById.value = nextCardsById;
    }
  }

  function removeSlickFromCards(slickId: number) {
    if (slickId <= 0) {
      return;
    }

    const nextCardsById: CardMap = {};
    let hasChanges = false;

    for (const [key, card] of Object.entries(cardsById.value)) {
      if (card.slickId === slickId) {
        hasChanges = true;
        nextCardsById[Number(key)] = {
          ...card,
          slickId: null,
          slickName: null,
          slick: null
        };
        continue;
      }

      nextCardsById[Number(key)] = card;
    }

    if (hasChanges) {
      cardsById.value = nextCardsById;
    }
  }

  async function runBusy<T>(
    operation: () => Promise<Result<T, AppError>>,
    options?: {
      boardId?: number;
      suppressError?: (error: AppError) => boolean;
    }
  ) {
    busy.value = true;
    try {
      const result = await operation();
      if (options?.boardId !== undefined && activeBoardId.value !== options.boardId) {
        return result;
      }

      if (!result.ok) {
        if (options?.suppressError?.(result.error)) {
          feedback.clearError();
        } else {
          reportError(result.error);
        }
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  return {
    cardsById,
    cardIdsByColumnId,
    busy,
    activeBoardId,
    replaceBoardCards,
    dispose,
    createCard,
    saveCard,
    transferCard,
    deleteCard,
    deleteCards,
    archiveCard,
    archiveCards,
    bulkMoveCards,
    bulkEditCards,
    moveCard,
    applyCreatedCard,
    upsertCard,
    upsertCards,
    removeCard,
    removeCards,
    getCardById,
    getCardsForColumn,
    removeTagFromCards,
    removeSlickFromCards
  };
});

function hasValidationErrors(error: AppError) {
  return Boolean(error.validationErrors && Object.keys(error.validationErrors).length > 0);
}

function cloneCard(card: Card): Card {
  return {
    ...card,
    tags: card.tags.map(tag => ({ ...tag })),
    tagNames: [...card.tagNames]
  };
}

function unlinkCardsFromColumns(source: CardIdsByColumnMap, excludedCardIds: Set<number>): CardIdsByColumnMap {
  const next: CardIdsByColumnMap = {};
  for (const [columnId, cardIds] of Object.entries(source)) {
    const remainingCardIds = cardIds.filter(cardId => !excludedCardIds.has(cardId));
    if (remainingCardIds.length > 0) {
      next[Number(columnId)] = remainingCardIds;
    }
  }

  return next;
}

function sortCardIds(cardIds: number[], cardsById: CardMap) {
  return [...new Set(cardIds)].sort((leftId, rightId) => {
    const leftCard = cardsById[leftId];
    const rightCard = cardsById[rightId];
    if (!leftCard || !rightCard) {
      return 0;
    }

    return compareSortKey(leftCard.sortKey, rightCard.sortKey);
  });
}

function compareSortKey(left: string, right: string) {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}

function resolvePositionAfterCardId(
  cardsById: CardMap,
  cardIdsByColumnId: CardIdsByColumnMap,
  movingCardId: number,
  targetColumnId: number,
  targetCardId: number | null
): number | null | undefined {
  const targetCardIds = cardIdsByColumnId[targetColumnId];
  if (!targetCardIds) {
    return targetCardId === null ? null : undefined;
  }

  if (targetCardId === movingCardId) {
    return undefined;
  }

  const filteredTargetCardIds = targetCardIds.filter(cardId => cardId !== movingCardId);
  if (targetCardId === null) {
    return filteredTargetCardIds.length === 0 ? null : filteredTargetCardIds[filteredTargetCardIds.length - 1];
  }

  const targetIndex = filteredTargetCardIds.findIndex(cardId => cardId === targetCardId);
  if (targetIndex < 0 || !cardsById[targetCardId]) {
    return undefined;
  }

  return targetIndex === 0 ? null : filteredTargetCardIds[targetIndex - 1];
}

function resolvePositionAfterCardIdForBulkMove(
  cardsById: CardMap,
  cardIdsByColumnId: CardIdsByColumnMap,
  movingCardIds: number[],
  targetColumnId: number,
  targetCardId: number | null
): number | null | undefined {
  const targetCardIds = cardIdsByColumnId[targetColumnId];
  if (!targetCardIds) {
    return targetCardId === null ? null : undefined;
  }

  const movingCardIdSet = new Set(movingCardIds);
  const filteredTargetCardIds = targetCardIds.filter(cardId => !movingCardIdSet.has(cardId));

  let resolvedTargetCardId = targetCardId;
  if (resolvedTargetCardId !== null && movingCardIdSet.has(resolvedTargetCardId)) {
    const originalIndex = targetCardIds.findIndex(cardId => cardId === resolvedTargetCardId);
    if (originalIndex < 0) {
      return undefined;
    }

    resolvedTargetCardId = targetCardIds
      .slice(originalIndex + 1)
      .find(cardId => !movingCardIdSet.has(cardId)) ?? null;
  }

  if (resolvedTargetCardId === null) {
    return filteredTargetCardIds.length === 0 ? null : filteredTargetCardIds[filteredTargetCardIds.length - 1];
  }

  const targetIndex = filteredTargetCardIds.findIndex(cardId => cardId === resolvedTargetCardId);
  if (targetIndex < 0 || !cardsById[resolvedTargetCardId]) {
    return undefined;
  }

  return targetIndex === 0 ? null : filteredTargetCardIds[targetIndex - 1];
}
