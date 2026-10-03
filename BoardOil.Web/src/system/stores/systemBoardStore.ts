import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { SystemBoardSummary } from '../../shared/types/boardTypes';

export const useSystemBoardStore = defineStore('systemBoard', () => {
  const boards = ref<SystemBoardSummary[]>([]);
  const busy = ref(false);
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();

  function SET_BOARDS(nextBoards: SystemBoardSummary[]) {
    boards.value = [...nextBoards].sort((left, right) => left.id - right.id);
  }

  async function loadBoards() {
    busy.value = true;
    try {
      const result = await api.getBoards();
      if (!result.ok) {
        feedback.setError(result.error.message, 'systemBoard');
        SET_BOARDS([]);
        return false;
      }

      SET_BOARDS(result.data);
      feedback.clearError('systemBoard');
      return true;
    } finally {
      busy.value = false;
    }
  }

  function dispose() {
    SET_BOARDS([]);
    busy.value = false;
  }

  return {
    boards,
    busy,
    loadBoards,
    dispose
  };
});
