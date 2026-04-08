import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { BoardSummary } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useBoardCatalogueStore = defineStore('boardCatalogue', () => {
  const boards = ref<BoardSummary[]>([]);
  const busy = ref(false);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  async function loadBoards() {
    const result = await api.getBoards();
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    boards.value = [...result.data].sort((left, right) => left.id - right.id);
    feedback.clearError();
    return true;
  }

  async function createBoard(name: string) {
    const result = await runBusy(() => api.createBoard(name));
    if (!result.ok) {
      return null;
    }

    const created = {
      id: result.data.id,
      name: result.data.name,
      createdAtUtc: result.data.createdAtUtc,
      updatedAtUtc: result.data.updatedAtUtc,
      currentUserRole: result.data.currentUserRole ?? null
    };
    boards.value = [...boards.value, created].sort((left, right) => left.id - right.id);
    return created;
  }

  async function importTasksMdBoard(url: string) {
    const result = await runBusy(() => api.importTasksMdBoard(url));
    if (!result.ok) {
      return null;
    }

    const created = {
      id: result.data.id,
      name: result.data.name,
      createdAtUtc: result.data.createdAtUtc,
      updatedAtUtc: result.data.updatedAtUtc,
      currentUserRole: result.data.currentUserRole ?? null
    };
    boards.value = [...boards.value, created].sort((left, right) => left.id - right.id);
    return created;
  }

  async function importBoardPackage(file: File, name?: string) {
    const result = await runBusy(() => api.importBoardPackage(file, name));
    if (!result.ok) {
      return null;
    }

    const created = {
      id: result.data.id,
      name: result.data.name,
      createdAtUtc: result.data.createdAtUtc,
      updatedAtUtc: result.data.updatedAtUtc,
      currentUserRole: result.data.currentUserRole ?? null
    };
    boards.value = [...boards.value, created].sort((left, right) => left.id - right.id);
    return created;
  }

  async function saveBoard(boardId: number, name: string) {
    const result = await runBusy(() => api.saveBoard(boardId, name));
    if (!result.ok) {
      return null;
    }

    boards.value = boards.value
      .map(board => (board.id === boardId ? result.data : board))
      .sort((left, right) => left.id - right.id);
    return result.data;
  }

  async function deleteBoard(boardId: number) {
    const result = await runBusy(() => api.deleteBoard(boardId));
    if (!result.ok) {
      return false;
    }

    boards.value = boards.value.filter(board => board.id !== boardId);
    return true;
  }

  function dispose() {
    boards.value = [];
    busy.value = false;
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

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  return {
    boards,
    busy,
    loadBoards,
    createBoard,
    importTasksMdBoard,
    importBoardPackage,
    saveBoard,
    deleteBoard,
    dispose
  };
});
