import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { sortBoard } from '../mappers/sortBoard';
import { createBoardRealtime } from '../realtime/boardRealtime';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useCardStore } from './cardStore';
import { useCardTypeStore } from './cardTypeStore';
import type { Board, BoardSummary, Column } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

type BoardShell = Omit<Board, 'columns'> & {
  columns: Column[];
};

export const useBoardStore = defineStore('board', () => {
  const boardShell = ref<BoardShell | null>(null);
  const busy = ref(false);
  const isLoadingBoard = ref(false);
  const currentBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const cardStore = useCardStore();
  const cardTypeStore = useCardTypeStore();
  const api = createBoardApi();
  const board = computed<Board | null>(() => {
    if (!boardShell.value) {
      return null;
    }

    return {
      ...boardShell.value,
      columns: boardShell.value.columns.map(column => ({
        ...column,
        cards: cardStore.getCardsForColumn(column.id)
      }))
    };
  });
  const currentUserRole = computed(() => boardShell.value?.currentUserRole ?? null);
  const isCurrentUserOwner = computed(() => currentUserRole.value === 'Owner');

  const realtime = createBoardRealtime({
    onColumnCreated: upsertColumn,
    onColumnUpdated: upsertColumn,
    onColumnDeleted: removeColumn,
    onCardCreated: cardStore.upsertCard,
    onCardUpdated: cardStore.upsertCard,
    onCardDeleted: cardStore.removeCard,
    onCardMoved: cardStore.upsertCard,
    onResync: async () => {
      if (currentBoardId.value !== null) {
        await loadBoard(currentBoardId.value);
        await cardTypeStore.loadCardTypes(currentBoardId.value);
      }
    }
  });
  let loadRequestVersion = 0;
  let initializeRequestVersion = 0;

  async function initialize(boardId: number) {
    const requestVersion = ++initializeRequestVersion;
    isLoadingBoard.value = true;
    try {
      const loaded = await loadBoard(boardId);
      if (!loaded) {
        return false;
      }

      try {
        await realtime.connect(boardId);
        return true;
      } catch {
        feedback.setError('Realtime connection failed.');
        await realtime.disconnect();
        return false;
      }
    } finally {
      if (requestVersion === initializeRequestVersion) {
        isLoadingBoard.value = false;
      }
    }
  }

  async function dispose() {
    initializeRequestVersion += 1;
    loadRequestVersion += 1;
    await realtime.disconnect();
    boardShell.value = null;
    currentBoardId.value = null;
    isLoadingBoard.value = false;
    cardStore.dispose();
  }

  async function loadBoard(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    const result = await api.getBoard(boardId);
    if (requestVersion !== loadRequestVersion) {
      return false;
    }

    if (!result.ok) {
      boardShell.value = null;
      currentBoardId.value = null;
      cardStore.dispose();
      reportError(result.error);
      return false;
    }

    const sortedBoard = sortBoard(result.data);
    currentBoardId.value = boardId;
    boardShell.value = stripBoardCards(sortedBoard);
    cardStore.replaceBoardCards(boardId, sortedBoard.columns);
    feedback.clearError();
    return true;
  }

  async function createColumn(title: string) {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.createColumn(boardId, trimmedTitle));
    if (!result.ok) {
      return;
    }

    upsertColumn(result.data);
  }

  async function saveColumn(columnId: number, title: string) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.saveColumn(boardId, columnId, title));
    if (!result.ok) {
      return;
    }

    upsertColumn(result.data);
  }

  async function moveColumn(columnId: number, positionAfterColumnId: number | null) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.moveColumn(boardId, columnId, positionAfterColumnId));
    if (!result.ok) {
      return;
    }

    upsertColumn(result.data);
  }

  async function deleteColumn(columnId: number) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.deleteColumn(boardId, columnId));
    if (!result.ok) {
      return;
    }

    removeColumn(columnId);
  }

  function applyBoardSummaryUpdate(summary: Pick<BoardSummary, 'id' | 'name' | 'description' | 'updatedAtUtc'>) {
    mutateBoardShell(draft => {
      if (draft.id !== summary.id) {
        return;
      }

      draft.name = summary.name;
      draft.description = summary.description;
      draft.updatedAtUtc = summary.updatedAtUtc;
    });
  }

  function getColumnById(columnId: number | null) {
    if (!boardShell.value || columnId === null) {
      return null;
    }

    return boardShell.value.columns.find(x => x.id === columnId) ?? null;
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

  function getCurrentBoardIdOrReport() {
    if (currentBoardId.value === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return currentBoardId.value;
  }

  function upsertColumn(column: Column) {
    mutateBoardShell(draft => {
      const existingIndex = draft.columns.findIndex(x => x.id === column.id);
      if (existingIndex >= 0) {
        draft.columns.splice(existingIndex, 1);
      }

      draft.columns.push(column);
      sortColumns(draft.columns);
    });
  }

  function removeColumn(columnId: number) {
    mutateBoardShell(draft => {
      const index = draft.columns.findIndex(x => x.id === columnId);
      if (index < 0) {
        return;
      }

      draft.columns.splice(index, 1);
    });
  }

  function mutateBoardShell(mutator: (draft: BoardShell) => void) {
    if (!boardShell.value) {
      return;
    }

    const draft = cloneBoardShell(boardShell.value);
    mutator(draft);
    boardShell.value = draft;
  }

  return {
    board,
    currentUserRole,
    isCurrentUserOwner,
    busy,
    isLoadingBoard,
    currentBoardId,
    initialize,
    dispose,
    createColumn,
    saveColumn,
    moveColumn,
    deleteColumn,
    applyBoardSummaryUpdate,
    getColumnById
  };
});

function stripBoardCards(source: Board): BoardShell {
  return {
    ...source,
    columns: source.columns.map(column => ({
      id: column.id,
      title: column.title,
      sortKey: column.sortKey,
      createdAtUtc: column.createdAtUtc,
      updatedAtUtc: column.updatedAtUtc
    }))
  };
}

function cloneBoardShell(source: BoardShell): BoardShell {
  return {
    ...source,
    columns: source.columns.map(column => ({ ...column }))
  };
}

function sortColumns(columns: Column[]) {
  columns.sort((left, right) => compareSortKey(left.sortKey, right.sortKey));
}

function compareSortKey(left: string, right: string) {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}
