import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import type { CardType, TagStyleName } from '../types/boardTypes';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useCardTypeStore = defineStore('cardType', () => {
  const cardTypes = ref<CardType[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  const systemCardType = computed(() => cardTypes.value.find(x => x.isSystem) ?? null);

  function dispose() {
    cardTypes.value = [];
    busy.value = false;
    activeBoardId.value = null;
  }

  async function loadCardTypes(boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      cardTypes.value = [];
      return false;
    }

    activeBoardId.value = resolvedBoardId;
    const result = await api.getCardTypes(resolvedBoardId);
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    cardTypes.value = sortCardTypes(result.data);
    feedback.clearError();
    return true;
  }

  async function createCardType(
    name: string,
    emoji: string | null | undefined,
    styleName: TagStyleName,
    stylePropertiesJson: string,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.createCardType(resolvedBoardId, name, emoji, styleName, stylePropertiesJson));
    if (!result.ok) {
      return null;
    }

    upsertCardType(result.data);
    return result.data;
  }

  async function updateCardType(
    cardTypeId: number,
    name: string,
    emoji: string | null | undefined,
    styleName: TagStyleName,
    stylePropertiesJson: string,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.updateCardType(
      resolvedBoardId,
      cardTypeId,
      name,
      emoji,
      styleName,
      stylePropertiesJson
    ));
    if (!result.ok) {
      return null;
    }

    upsertCardType(result.data);
    return result.data;
  }

  async function deleteCardType(cardTypeId: number, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return false;
    }

    const result = await runBusy(() => api.deleteCardType(resolvedBoardId, cardTypeId));
    if (!result.ok) {
      return false;
    }

    removeCardType(cardTypeId);
    return true;
  }

  function getCardTypeById(cardTypeId: number | null) {
    if (cardTypeId === null) {
      return null;
    }

    return cardTypes.value.find(x => x.id === cardTypeId) ?? null;
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function upsertCardType(cardType: CardType) {
    const existingIndex = cardTypes.value.findIndex(x => x.id === cardType.id);
    if (existingIndex < 0) {
      cardTypes.value = sortCardTypes([...cardTypes.value, cardType]);
      return;
    }

    const next = [...cardTypes.value];
    next[existingIndex] = cardType;
    cardTypes.value = sortCardTypes(next);
  }

  function removeCardType(cardTypeId: number) {
    cardTypes.value = cardTypes.value.filter(x => x.id !== cardTypeId);
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  function resolveBoardId(boardId: number | null) {
    const resolved = boardId ?? activeBoardId.value;
    if (resolved === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return resolved;
  }

  return {
    cardTypes,
    busy,
    activeBoardId,
    systemCardType,
    dispose,
    loadCardTypes,
    createCardType,
    updateCardType,
    deleteCardType,
    getCardTypeById
  };
});

function sortCardTypes(cardTypes: CardType[]) {
  return [...cardTypes].sort((left, right) => {
    if (left.isSystem && !right.isSystem) {
      return -1;
    }

    if (!left.isSystem && right.isSystem) {
      return 1;
    }

    return left.name.localeCompare(right.name);
  });
}
