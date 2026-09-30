import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import type { CardType, CardTypeEditModel } from '../../shared/types/boardTypes';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useCardTypeStore = defineStore('cardType', () => {
  const cardTypes = ref<CardType[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  const systemCardType = computed(() => cardTypes.value.find(x => x.isSystem) ?? null);

  function SET_CARD_TYPES(boardId: number, nextCardTypes: CardType[]) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    cardTypes.value = sortCardTypes(nextCardTypes);
  }

  function UPSERT_CARD_TYPE(boardId: number, cardType: CardType) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    const existingIndex = cardTypes.value.findIndex(x => x.id === cardType.id);
    if (existingIndex < 0) {
      cardTypes.value = sortCardTypes([...cardTypes.value, cardType]);
      return;
    }

    const next = [...cardTypes.value];
    next[existingIndex] = cardType;
    cardTypes.value = sortCardTypes(next);
  }

  function REMOVE_CARD_TYPE(boardId: number, cardTypeId: number) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    cardTypes.value = cardTypes.value.filter(x => x.id !== cardTypeId);
  }

  function dispose() {
    cardTypes.value = [];
    busy.value = false;
    activeBoardId.value = null;
  }

  async function loadCardTypes(boardId: number) {
    if (activeBoardId.value !== boardId) {
      cardTypes.value = [];
    }

    activeBoardId.value = boardId;
    const result = await api.getCardTypes(boardId);
    if (activeBoardId.value !== boardId) {
      return false;
    }

    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    SET_CARD_TYPES(boardId, result.data);
    feedback.clearError();
    return true;
  }

  async function createCardType(
    model: CardTypeEditModel,
    boardId: number
  ) {
    const result = await runBusy(() => api.createCardType(boardId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_CARD_TYPE(boardId, result.data);
    return result.data;
  }

  async function updateCardType(
    cardTypeId: number,
    model: CardTypeEditModel,
    boardId: number
  ) {
    const result = await runBusy(() => api.updateCardType(boardId, cardTypeId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_CARD_TYPE(boardId, result.data);
    return result.data;
  }

  async function deleteCardType(cardTypeId: number, boardId: number) {
    const result = await runBusy(() => api.deleteCardType(boardId, cardTypeId));
    if (!result.ok) {
      return false;
    }

    REMOVE_CARD_TYPE(boardId, cardTypeId);
    return true;
  }

  async function setDefaultCardType(cardTypeId: number, boardId: number) {
    const result = await runBusy(() => api.setDefaultCardType(boardId, cardTypeId));
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return true;
    }

    await loadCardTypes(boardId);
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

  function reportError(error: AppError) {
    feedback.setError(error.message);
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
    setDefaultCardType,
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
