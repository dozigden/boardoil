import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { BoardEditModel, BoardSummary } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useBoardCatalogueStore = defineStore('boardCatalogue', () => {
  const boards = ref<BoardSummary[]>([]);
  const busy = ref(false);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function SET_BOARDS(nextBoards: BoardSummary[]) {
    boards.value = [...nextBoards].sort((left, right) => left.id - right.id);
  }

  function UPSERT_BOARD(board: BoardSummary) {
    SET_BOARDS([...boards.value.filter(existing => existing.id !== board.id), board]);
  }

  function REMOVE_BOARD(boardId: number) {
    boards.value = boards.value.filter(board => board.id !== boardId);
  }

  async function loadBoards() {
    const result = await api.getBoards();
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    SET_BOARDS(result.data);
    feedback.clearError('boardCatalogue');
    return true;
  }

  async function createBoard(name: string, description?: string) {
    const result = await runBusy(() => api.createBoard(name, description));
    if (!result.ok) {
      return null;
    }

    const created = toBoardSummary(result.data);
    UPSERT_BOARD(created);
    return created;
  }

  async function cloneBoard(sourceBoardId: number, name: string) {
    const result = await runBusy(() => api.cloneBoard(sourceBoardId, name));
    if (!result.ok) {
      return null;
    }

    const created = toBoardSummary(result.data);
    UPSERT_BOARD(created);
    return created;
  }

  async function importBoardPackage(file: File, name?: string) {
    const result = await runBusy(() => api.importBoardPackage(file, name));
    if (!result.ok) {
      return null;
    }

    const created = toBoardSummary(result.data);
    UPSERT_BOARD(created);
    return created;
  }

  async function saveBoard(boardId: number, model: BoardEditModel) {
    const result = await runBusy(() => api.saveBoard(boardId, model));
    if (!result.ok) {
      return null;
    }

    UPSERT_BOARD(result.data);
    return result.data;
  }

  async function deleteBoard(boardId: number) {
    const result = await runBusy(() => api.deleteBoard(boardId));
    if (!result.ok) {
      return false;
    }

    REMOVE_BOARD(boardId);
    return true;
  }

  function dispose() {
    SET_BOARDS([]);
    busy.value = false;
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError('boardCatalogue');
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function reportError(error: AppError) {
    feedback.setError(error.message, 'boardCatalogue');
  }

  return {
    boards,
    busy,
    loadBoards,
    createBoard,
    cloneBoard,
    importBoardPackage,
    saveBoard,
    deleteBoard,
    dispose
  };
});

function toBoardSummary(board: BoardSummary): BoardSummary {
  return {
    id: board.id,
    name: board.name,
    description: board.description,
    slickCohesionModeEnabled: board.slickCohesionModeEnabled,
    cardAttachmentThumbnailsEnabled: board.cardAttachmentThumbnailsEnabled,
    createdAtUtc: board.createdAtUtc,
    updatedAtUtc: board.updatedAtUtc,
    currentUserRole: board.currentUserRole ?? null
  };
}
