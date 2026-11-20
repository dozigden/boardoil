import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createJobsApi } from '../../shared/api/jobsApi';
import { createSystemApi } from '../../shared/api/systemApi';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { DeletedBoardSummary, SystemBoardSummary } from '../../shared/types/boardTypes';

export const useSystemBoardStore = defineStore('systemBoard', () => {
  const boards = ref<SystemBoardSummary[]>([]);
  const deletedBoards = ref<DeletedBoardSummary[]>([]);
  const busy = ref(false);
  const purgingBoardIds = ref<number[]>([]);
  const boardCatalogueStore = useBoardCatalogueStore();
  const feedback = useUiFeedbackStore();
  const api = createSystemApi();
  const jobsApi = createJobsApi();

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

  function SET_BOARD_PURGE_PENDING(boardId: number, pending: boolean) {
    const otherIds = purgingBoardIds.value.filter(id => id !== boardId);
    purgingBoardIds.value = pending ? [...otherIds, boardId] : otherIds;
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

  async function queueBoardPurge(boardId: number) {
    if (busy.value || purgingBoardIds.value.includes(boardId)) return;

    SET_BOARD_PURGE_PENDING(boardId, true);
    try {
      const result = await api.queueBoardPurge(boardId);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return;
      }

      feedback.clearError('systemBoard');
      feedback.showToast('Board purge queued.');
      await observeBoardPurge(boardId, result.data.id);
    } finally {
      SET_BOARD_PURGE_PENDING(boardId, false);
    }
  }

  async function observeBoardPurge(boardId: number, jobId: number) {
    while (true) {
      await new Promise<void>(resolve => setTimeout(resolve, 2_000));
      const result = await jobsApi.getJobDetails(jobId);
      if (!result.ok) {
        feedback.showToast('Could not check the queued board purge.', 'error');
        return;
      }

      const job = result.data;
      if (job.status === 'pending' || job.status === 'running') continue;

      if (job.status === 'completed') {
        SET_DELETED_BOARDS(deletedBoards.value.filter(board => board.id !== boardId));
        const outcome = JSON.parse(job.resultJson) as { skipped: boolean };
        if (outcome.skipped) feedback.showToast('Board purge skipped.');
        else feedback.showToast('Board purged.');
      } else {
        feedback.showToast(job.errorMessage ?? 'Board purge did not complete.', 'error');
        const deleted = await api.getDeletedBoards();
        if (deleted.ok) SET_DELETED_BOARDS(deleted.data);
      }
      return;
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
    purgingBoardIds,
    loadBoards,
    restoreBoard,
    queueBoardPurge,
    dispose
  };
});
