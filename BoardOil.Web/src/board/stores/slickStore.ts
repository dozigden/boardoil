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
  let loadRequestVersion = 0;

  function dispose() {
    loadRequestVersion += 1;
    activeBoardId.value = null;
    slicks.value = [];
    busy.value = false;
  }

  async function loadSlicks(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    if (activeBoardId.value !== boardId) {
      slicks.value = [];
    }

    activeBoardId.value = boardId;
    const result = await api.getSlicks(boardId);
    if (requestVersion !== loadRequestVersion) {
      return false;
    }

    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    slicks.value = [...result.data].sort((a, b) => a.name.localeCompare(b.name));
    feedback.clearError();
    return true;
  }

  async function createSlick(
    model: SlickEditModel,
    boardId: number
  ) {
    const result = await runBusy(() => api.createSlick(boardId, model));
    if (!result.ok) {
      return null;
    }

    upsertSlick(boardId, result.data);
    return result.data;
  }

  async function getCreateDefaultStyle(boardId: number): Promise<StyleDefault | null> {
    const result = await runBusy(() => api.getSlickCreateDefaultStyle(boardId));
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
    const result = await runBusy(() => api.updateSlick(boardId, slickId, model));
    if (!result.ok) {
      return null;
    }

    upsertSlick(boardId, result.data);
    return result.data;
  }

  async function deleteSlick(slickId: number, boardId: number) {
    const result = await runBusy(() => api.deleteSlick(boardId, slickId));
    if (!result.ok) {
      return false;
    }

    if (activeBoardId.value !== boardId) {
      return true;
    }

    slicks.value = slicks.value.filter(x => x.id !== slickId);
    return true;
  }

  function getSlickById(slickId: number | null) {
    if (slickId === null) {
      return null;
    }

    return slicks.value.find(x => x.id === slickId) ?? null;
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

  function upsertSlick(boardId: number, slick: Slick) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    const existingIndex = slicks.value.findIndex(x => x.id === slick.id);
    if (existingIndex < 0) {
      slicks.value = [...slicks.value, slick].sort((a, b) => a.name.localeCompare(b.name));
      return;
    }

    const next = [...slicks.value];
    next[existingIndex] = slick;
    slicks.value = next.sort((a, b) => a.name.localeCompare(b.name));
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
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
