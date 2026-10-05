import type { CardFieldDefinitionInput } from '../../shared/types/cardFieldTypes';
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
  const saveError = ref<AppError | null>(null);
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

  function SET_SAVE_ERROR(error: AppError | null) {
    saveError.value = error;
  }

  function clearSaveError() {
    SET_SAVE_ERROR(null);
  }

  function dispose() {
    clearSaveError();
    cardTypes.value = [];
    busy.value = false;
    activeBoardId.value = null;
  }

  async function loadCardTypes(boardId: number) {
    if (activeBoardId.value !== boardId) {
      clearSaveError();
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
    feedback.clearError('cardType');
    return true;
  }

  async function createCardType(
    model: CardTypeEditModel,
    boardId: number
  ) {
    const result = await runBusy(boardId, () => api.createCardType(boardId, model));
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
    const result = await runBusy(boardId, () => api.updateCardType(boardId, cardTypeId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_CARD_TYPE(boardId, result.data);
    return result.data;
  }

  function previewFields(boardId: number, cardTypeId: number, fields: CardFieldDefinitionInput[]) {
    return api.previewCardTypeFields(boardId, cardTypeId, fields);
  }

  async function deleteCardType(cardTypeId: number, boardId: number) {
    const result = await runBusy(boardId, () => api.deleteCardType(boardId, cardTypeId));
    if (!result.ok) {
      return false;
    }

    REMOVE_CARD_TYPE(boardId, cardTypeId);
    return true;
  }

  async function setDefaultCardType(cardTypeId: number, boardId: number) {
    const result = await runBusy(boardId, () => api.setDefaultCardType(boardId, cardTypeId), 'page');
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

  async function runBusy<T>(boardId: number, operation: () => Promise<Result<T, AppError>>, errorTarget: 'dialog' | 'page' = 'dialog') {
    if (activeBoardId.value === boardId) {
      busy.value = true;
      clearSaveError();
      feedback.clearError('cardType');
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        if (errorTarget === 'dialog') {
          SET_SAVE_ERROR(result.error);
        } else {
          reportError(result.error);
        }
      } else {
        feedback.clearError('cardType');
      }

      return result;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
    }
  }

  function reportError(error: AppError) {
    feedback.setError(error.message, 'cardType');
  }

  return {
    cardTypes,
    busy,
    saveError,
    clearSaveError,
    activeBoardId,
    systemCardType,
    dispose,
    loadCardTypes,
    createCardType,
    updateCardType,
    previewFields,
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
