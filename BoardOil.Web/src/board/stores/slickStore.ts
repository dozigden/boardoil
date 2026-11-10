import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Slick, SlickEditModel, StyleDefault } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useSlickStore = defineStore('slick', () => {
  const slicks = ref<Slick[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function SET_SLICKS(boardId: number, nextSlicks: Slick[]) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    slicks.value = sortSlicks(nextSlicks);
  }

  function UPSERT_SLICK(boardId: number, slick: Slick) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    const existingIndex = slicks.value.findIndex(x => x.id === slick.id);
    if (existingIndex < 0) {
      slicks.value = sortSlicks([...slicks.value, slick]);
      return;
    }

    const next = [...slicks.value];
    next[existingIndex] = slick;
    slicks.value = sortSlicks(next);
  }

  function REMOVE_SLICK(boardId: number, slickId: number) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    slicks.value = slicks.value.filter(x => x.id !== slickId);
  }

  function dispose() {
    activeBoardId.value = null;
    slicks.value = [];
    busy.value = false;
  }

  async function loadSlicks(boardId: number) {
    if (activeBoardId.value !== boardId) {
      slicks.value = [];
    }

    activeBoardId.value = boardId;
    const result = await api.getSlicks(boardId);
    if (activeBoardId.value !== boardId) {
      return false;
    }

    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    SET_SLICKS(boardId, result.data);
    feedback.clearError('slick');
    return true;
  }

  async function createSlick(
    model: SlickEditModel,
    boardId: number
  ) {
    const result = await runApiOperation(boardId, () => api.createSlick(boardId, model), { suppressError: true });
    if (result.ok) UPSERT_SLICK(boardId, result.data);
    return result;
  }

  async function getCreateDefaultStyle(boardId: number): Promise<StyleDefault | null> {
    const result = await runApiOperation(boardId, () => api.getSlickCreateDefaultStyle(boardId));
    if (!result.ok) {
      return null;
    }

    return result.data;
  }

  async function updateSlick(
    slickId: number,
    model: SlickEditModel,
    boardId: number
  ) {
    const result = await runApiOperation(boardId, () => api.updateSlick(boardId, slickId, model), { suppressError: true });
    if (result.ok) UPSERT_SLICK(boardId, result.data);
    return result;
  }

  async function deleteSlick(slickId: number, boardId: number) {
    const result = await runApiOperation(boardId, () => api.deleteSlick(boardId, slickId), { suppressError: true });
    if (result.ok) REMOVE_SLICK(boardId, slickId);
    return result;
  }

  function getSlickById(slickId: number | null) {
    if (slickId === null) {
      return null;
    }

    return slicks.value.find(x => x.id === slickId) ?? null;
  }

  async function runApiOperation<T>(boardId: number, operation: () => Promise<Result<T, AppError>>, options: { suppressError?: boolean } = {}) {
    if (activeBoardId.value === boardId) {
      busy.value = true;
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        if (!options.suppressError) reportError(result.error);
      } else {
        feedback.clearError('slick');
      }

      return result;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
    }
  }

  function upsertSlick(boardId: number, slick: Slick) {
    UPSERT_SLICK(boardId, slick);
  }

  function reportError(error: AppError) {
    feedback.setError(error.message, 'slick');
  }

  return {
    slicks,
    busy,
    activeBoardId,
    dispose,
    loadSlicks,
    upsertSlick,
    getCreateDefaultStyle,
    createSlick,
    updateSlick,
    deleteSlick,
    getSlickById
  };
});

function sortSlicks(slicks: Slick[]) {
  return [...slicks].sort((left, right) => left.name.localeCompare(right.name));
}
