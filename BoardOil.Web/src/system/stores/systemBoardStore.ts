import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { DeletedBoardSummary, SystemBoardSummary } from '../../shared/types/boardTypes';

export const useSystemBoardStore = defineStore('systemBoard', () => {
  const boards = ref<SystemBoardSummary[]>([]);
  const deletedBoards = ref<DeletedBoardSummary[]>([]);
  const busy = ref(false);
  const boardCatalogueStore = useBoardCatalogueStore();
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();

  function SET_BOARDS(nextBoards: SystemBoardSummary[]) {
    boards.value = [...nextBoards].sort((left, right) => left.id - right.id);
  }

  function SET_DELETED_BOARDS(nextBoards: DeletedBoardSummary[]) {
    deletedBoards.value = nextBoards;
  }

  function RESTORE_BOARD(board: SystemBoardSummary) {
    SET_DELETED_BOARDS(deletedBoards.value.filter(deleted => deleted.id !== board.id));
    SET_BOARDS([...boards.value.filter(active => active.id !== board.id), board]);
  }

  async function loadBoards() {
    busy.value = true;
    try {
      const [active, deleted] = await Promise.all([api.getBoards(), api.getDeletedBoards()]);
      SET_BOARDS(active.ok ? active.data : []);
      SET_DELETED_BOARDS(deleted.ok ? deleted.data : []);
      const failure = [active, deleted].find(result => !result.ok);
      if (failure && !failure.ok) {
        feedback.setError(failure.error.message, 'systemBoard');
        return false;
      }

      feedback.clearError('systemBoard');
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function restoreBoard(boardId: number) {
    busy.value = true;
    try {
      const result = await api.restoreBoard(boardId);
      if (result.ok) {
        RESTORE_BOARD(result.data);
        feedback.clearError('systemBoard');
        feedback.showToast('Board restored.');
        await boardCatalogueStore.loadBoards({ suppressError: true });
      } else {
        feedback.setError(result.error.message, 'systemBoard');
      }
    } finally {
      busy.value = false;
    }
  }

  function dispose() {
    SET_BOARDS([]);
    SET_DELETED_BOARDS([]);
    busy.value = false;
  }

  return {
    boards,
    deletedBoards,
    busy,
    loadBoards,
    restoreBoard,
    dispose
  };
});
