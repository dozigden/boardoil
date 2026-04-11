import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../api/systemApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { SystemBoardSummary } from '../types/boardTypes';

export const useSystemBoardStore = defineStore('systemBoard', () => {
  const boards = ref<SystemBoardSummary[]>([]);
  const busy = ref(false);
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();

  async function loadBoards() {
    busy.value = true;
    try {
      const result = await api.getBoards();
      if (!result.ok) {
        feedback.setError(result.error.message);
        boards.value = [];
        return false;
      }

      boards.value = [...result.data].sort((left, right) => left.id - right.id);
      feedback.clearError();
      return true;
    } finally {
      busy.value = false;
    }
  }

  function dispose() {
    boards.value = [];
    busy.value = false;
  }

  return {
    boards,
    busy,
    loadBoards,
    dispose
  };
});
