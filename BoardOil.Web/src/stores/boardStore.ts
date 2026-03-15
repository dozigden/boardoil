import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { sortBoard } from '../mappers/sortBoard';
import { createBoardRealtime } from '../realtime/boardRealtime';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { Board } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useBoardStore = defineStore('board', () => {
  const board = ref<Board | null>(null);
  const busy = ref(false);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  const realtime = createBoardRealtime(loadBoard);
  let dragState: { cardId: number; fromColumnId: number } | null = null;
  let initialized = false;

  async function initialize() {
    if (initialized) {
      return;
    }

    initialized = true;
    const loaded = await loadBoard();
    if (!loaded) {
      initialized = false;
      return;
    }

    try {
      await realtime.connect();
    } catch {
      feedback.setError('Realtime connection failed.');
      initialized = false;
    }
  }

  async function dispose() {
    await realtime.disconnect();
    initialized = false;
  }

  async function loadBoard() {
    const result = await api.getBoard();
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    board.value = sortBoard(result.data);
    feedback.clearError();
    return true;
  }

  async function createColumn(title: string) {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return false;
    }

    const result = await runBusy(() => api.createColumn(trimmedTitle));
    if (!result.ok) {
      return false;
    }

    return loadBoard();
  }

  async function saveColumn(columnId: number, title: string) {
    await runBusy(() => api.saveColumn(columnId, title));
  }

  async function deleteColumn(columnId: number) {
    const result = await runBusy(() => api.deleteColumn(columnId));
    if (!result.ok) {
      return;
    }

    await loadBoard();
  }

  async function createCard(columnId: number, title: string) {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return false;
    }

    const result = await runBusy(() => api.createCard(columnId, trimmedTitle));
    if (!result.ok) {
      return false;
    }

    return loadBoard();
  }

  async function saveCard(cardId: number, title: string, description: string) {
    const result = await runBusy(() => api.saveCard(cardId, title, description));
    if (!result.ok) {
      return;
    }

    await realtime.stopTyping(cardId, 'title');
    await realtime.stopTyping(cardId, 'description');
  }

  async function deleteCard(cardId: number) {
    const result = await runBusy(() => api.deleteCard(cardId));
    if (!result.ok) {
      return;
    }

    await loadBoard();
  }

  function startDrag(cardId: number, fromColumnId: number) {
    dragState = { cardId, fromColumnId };
  }

  async function dropCard(targetColumnId: number, position: number) {
    if (!dragState) {
      return;
    }

    const movingCardId = dragState.cardId;
    dragState = null;

    const result = await runBusy(() => api.moveCard(movingCardId, targetColumnId, position));
    if (!result.ok) {
      return;
    }
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
    board,
    busy,
    typingSummary: realtime.typingSummary,
    initialize,
    dispose,
    createColumn,
    saveColumn,
    deleteColumn,
    createCard,
    saveCard,
    deleteCard,
    startDrag,
    dropCard,
    announceTyping: realtime.announceTyping,
    stopTyping: realtime.stopTyping
  };
});
