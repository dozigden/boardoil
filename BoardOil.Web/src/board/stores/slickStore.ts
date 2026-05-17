import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Slick, SlickStyleName } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useSlickStore = defineStore('slick', () => {
  const slicks = ref<Slick[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function dispose() {
    activeBoardId.value = null;
    slicks.value = [];
    busy.value = false;
  }

  async function loadSlicks(boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      slicks.value = [];
      return false;
    }

    activeBoardId.value = resolvedBoardId;
    const result = await api.getSlicks(resolvedBoardId);
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    slicks.value = [...result.data].sort((a, b) => a.name.localeCompare(b.name));
    feedback.clearError();
    return true;
  }

  async function createSlick(
    name: string,
    styleName?: SlickStyleName,
    stylePropertiesJson?: string,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.createSlick(resolvedBoardId, name, styleName, stylePropertiesJson));
    if (!result.ok) {
      return null;
    }

    upsertSlick(result.data);
    return result.data;
  }

  async function updateSlick(
    slickId: number,
    name: string,
    styleName: SlickStyleName,
    stylePropertiesJson: string,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.updateSlick(resolvedBoardId, slickId, name, styleName, stylePropertiesJson));
    if (!result.ok) {
      return null;
    }

    upsertSlick(result.data);
    return result.data;
  }

  async function deleteSlick(slickId: number, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return false;
    }

    const result = await runBusy(() => api.deleteSlick(resolvedBoardId, slickId));
    if (!result.ok) {
      return false;
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

  function upsertSlick(slick: Slick) {
    const existingIndex = slicks.value.findIndex(x => x.id === slick.id || x.name === slick.name);
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

  function resolveBoardId(boardId: number | null) {
    const resolved = boardId ?? activeBoardId.value;
    if (resolved === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return resolved;
  }

  return {
    slicks,
    busy,
    activeBoardId,
    dispose,
    loadSlicks,
    createSlick,
    updateSlick,
    deleteSlick,
    getSlickById
  };
});
