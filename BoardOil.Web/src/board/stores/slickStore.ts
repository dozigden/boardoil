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
    const result = await runBusy(boardId, () => api.createSlick(boardId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_SLICK(boardId, result.data);
    return result.data;
  }

  async function getCreateDefaultStyle(boardId: number): Promise<StyleDefault | null> {
    const result = await runBusy(boardId, () => api.getSlickCreateDefaultStyle(boardId));
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
    const result = await runBusy(boardId, () => api.updateSlick(boardId, slickId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_SLICK(boardId, result.data);
    return result.data;
  }

  async function deleteSlick(slickId: number, boardId: number) {
    const result = await runBusy(boardId, () => api.deleteSlick(boardId, slickId));
    if (!result.ok) {
      return false;
    }

    REMOVE_SLICK(boardId, slickId);
    return true;
  }

  function getSlickById(slickId: number | null) {
    if (slickId === null) {
      return null;
    }

    return slicks.value.find(x => x.id === slickId) ?? null;
  }

  async function runBusy<T>(boardId: number, operation: () => Promise<Result<T, AppError>>) {
    if (activeBoardId.value === boardId) {
      busy.value = true;
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        reportError(result.error);
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
