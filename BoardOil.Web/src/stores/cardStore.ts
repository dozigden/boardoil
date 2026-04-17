import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { BoardColumn, Card } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

type CardMap = Record<number, Card>;
type CardIdsByColumnMap = Record<number, number[]>;

export const useCardStore = defineStore('card', () => {
  const cardsById = ref<CardMap>({});
  const cardIdsByColumnId = ref<CardIdsByColumnMap>({});
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();
  let dragState: { cardId: number; fromColumnId: number } | null = null;

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
    dragState = null;
  }

  function dispose() {
    activeBoardId.value = null;
    cardsById.value = {};
    cardIdsByColumnId.value = {};
    busy.value = false;
    dragState = null;
  }

  async function createCard(
    columnId: number,
    title: string,
    cardTypeId: number | null = null,
    boardId: number | null = activeBoardId.value
  ) {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return null;
    }

    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(
      () => {
        if (cardTypeId === null) {
          return api.createCard(resolvedBoardId, columnId, trimmedTitle);
        }

        return api.createCard(resolvedBoardId, columnId, trimmedTitle, cardTypeId);
      },
      {
        suppressError: error => hasValidationErrors(error)
      }
    );
    if (!result.ok) {
      return result;
    }

    upsertCard(result.data);
    return result;
  }

  async function saveCard(
    cardId: number,
    title: string,
    description: string,
    tagNames: string[],
    cardTypeId: number,
    boardColumnId: number,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return;
    }

    const result = await runBusy(() => api.saveCard(resolvedBoardId, cardId, title, description, tagNames, cardTypeId, boardColumnId));
    if (!result.ok) {
      return;
    }

    upsertCard(result.data);
  }

  async function deleteCard(cardId: number, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return;
    }

    const result = await runBusy(() => api.deleteCard(resolvedBoardId, cardId));
    if (!result.ok) {
      return;
    }

    removeCard(cardId);
  }

  function startDrag(cardId: number, fromColumnId: number) {
    dragState = { cardId, fromColumnId };
  }

  async function dropCard(
    targetColumnId: number,
    targetCardId: number | null,
    boardId: number | null = activeBoardId.value
  ) {
    if (!dragState) {
      return;
    }

    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      dragState = null;
      return;
    }

    const movingCardId = dragState.cardId;
    dragState = null;

    const positionAfterCardId = resolvePositionAfterCardId(
      cardsById.value,
      cardIdsByColumnId.value,
      movingCardId,
      targetColumnId,
      targetCardId
    );
    if (positionAfterCardId === undefined) {
      return;
    }

    const result = await runBusy(() => api.moveCard(resolvedBoardId, movingCardId, targetColumnId, positionAfterCardId));
    if (!result.ok) {
      return;
    }

    upsertCard(result.data);
  }

  function upsertCard(card: Card) {
    const nextCardsById: CardMap = {
      ...cardsById.value,
      [card.id]: cloneCard(card)
    };
    const nextCardIdsByColumnId = cloneCardIdsByColumn(cardIdsByColumnId.value);

    removeCardIdFromColumns(nextCardIdsByColumnId, card.id);
    const targetCardIds = nextCardIdsByColumnId[card.boardColumnId] ?? [];
    targetCardIds.push(card.id);
    nextCardIdsByColumnId[card.boardColumnId] = sortCardIds(targetCardIds, nextCardsById);

    cardsById.value = nextCardsById;
    cardIdsByColumnId.value = nextCardIdsByColumnId;
  }

  function removeCard(cardId: number) {
    const nextCardsById = { ...cardsById.value };
    delete nextCardsById[cardId];

    const nextCardIdsByColumnId = cloneCardIdsByColumn(cardIdsByColumnId.value);
    removeCardIdFromColumns(nextCardIdsByColumnId, cardId);

    cardsById.value = nextCardsById;
    cardIdsByColumnId.value = nextCardIdsByColumnId;
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

  async function runBusy<T>(
    operation: () => Promise<Result<T, AppError>>,
    options?: { suppressError?: (error: AppError) => boolean }
  ) {
    busy.value = true;
    try {
      const result = await operation();
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

  function resolveBoardId(boardId: number | null) {
    const resolvedBoardId = boardId ?? activeBoardId.value;
    if (resolvedBoardId === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return resolvedBoardId;
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
    deleteCard,
    startDrag,
    dropCard,
    upsertCard,
    removeCard,
    getCardById,
    getCardsForColumn,
    removeTagFromCards
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

function cloneCardIdsByColumn(source: CardIdsByColumnMap): CardIdsByColumnMap {
  const next: CardIdsByColumnMap = {};
  for (const [columnId, cardIds] of Object.entries(source)) {
    next[Number(columnId)] = [...cardIds];
  }

  return next;
}

function removeCardIdFromColumns(cardIdsByColumnId: CardIdsByColumnMap, cardId: number) {
  for (const [columnId, cardIds] of Object.entries(cardIdsByColumnId)) {
    const nextCardIds = cardIds.filter(existingCardId => existingCardId !== cardId);
    if (nextCardIds.length === 0) {
      delete cardIdsByColumnId[Number(columnId)];
      continue;
    }

    cardIdsByColumnId[Number(columnId)] = nextCardIds;
  }
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
