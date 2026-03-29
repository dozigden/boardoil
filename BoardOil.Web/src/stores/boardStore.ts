import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { sortBoard } from '../mappers/sortBoard';
import { createBoardRealtime } from '../realtime/boardRealtime';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { Board, BoardColumn, Card, Column } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useBoardStore = defineStore('board', () => {
  const board = ref<Board | null>(null);
  const busy = ref(false);
  const isLoadingBoard = ref(false);
  const currentBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  const realtime = createBoardRealtime({
    onColumnCreated: upsertColumn,
    onColumnUpdated: upsertColumn,
    onColumnDeleted: removeColumn,
    onCardCreated: upsertCard,
    onCardUpdated: upsertCard,
    onCardDeleted: removeCard,
    onCardMoved: upsertCard,
    onResync: async () => {
      if (currentBoardId.value !== null) {
        await loadBoard(currentBoardId.value);
      }
    }
  });
  let dragState: { cardId: number; fromColumnId: number } | null = null;
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
    board.value = null;
    currentBoardId.value = null;
    dragState = null;
    isLoadingBoard.value = false;
  }

  async function loadBoard(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    const result = await api.getBoard(boardId);
    if (requestVersion !== loadRequestVersion) {
      return false;
    }

    if (!result.ok) {
      board.value = null;
      currentBoardId.value = null;
      dragState = null;
      reportError(result.error);
      return false;
    }

    currentBoardId.value = boardId;
    board.value = sortBoard(result.data);
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

  async function createCard(columnId: number, title: string) {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.createCard(boardId, columnId, trimmedTitle));
    if (!result.ok) {
      return;
    }

    upsertCard(result.data);
  }

  async function saveCard(cardId: number, title: string, description: string, tagNames: string[]) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.saveCard(boardId, cardId, title, description, tagNames));
    if (!result.ok) {
      return;
    }

    upsertCard(result.data);
  }

  async function deleteCard(cardId: number) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.deleteCard(boardId, cardId));
    if (!result.ok) {
      return;
    }

    removeCard(cardId);
  }

  function startDrag(cardId: number, fromColumnId: number) {
    dragState = { cardId, fromColumnId };
  }

  async function dropCard(targetColumnId: number, targetCardId: number | null) {
    if (!dragState) {
      return;
    }

    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      dragState = null;
      return;
    }

    const movingCardId = dragState.cardId;
    dragState = null;

    const positionAfterCardId = resolvePositionAfterCardId(
      board.value,
      movingCardId,
      targetColumnId,
      targetCardId
    );
    if (positionAfterCardId === undefined) {
      return;
    }

    const result = await runBusy(() => api.moveCard(boardId, movingCardId, targetColumnId, positionAfterCardId));
    if (!result.ok) {
      return;
    }

    upsertCard(result.data);
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
    mutateBoard(draft => {
      const existingIndex = draft.columns.findIndex(x => x.id === column.id);
      const nextColumn: BoardColumn = {
        ...column,
        cards: existingIndex >= 0 ? draft.columns[existingIndex].cards : []
      };

      if (existingIndex >= 0) {
        draft.columns.splice(existingIndex, 1);
      }

      draft.columns.push(nextColumn);
      sortColumns(draft.columns);
    });
  }

  function removeColumn(columnId: number) {
    mutateBoard(draft => {
      const index = draft.columns.findIndex(x => x.id === columnId);
      if (index < 0) {
        return;
      }

      draft.columns.splice(index, 1);
    });
  }

  function upsertCard(card: Card) {
    mutateBoard(draft => {
      let existingColumn: BoardColumn | null = null;
      let existingIndex = -1;
      for (const column of draft.columns) {
        const cardIndex = column.cards.findIndex(x => x.id === card.id);
        if (cardIndex >= 0) {
          existingColumn = column;
          existingIndex = cardIndex;
          break;
        }
      }

      if (existingColumn) {
        existingColumn.cards.splice(existingIndex, 1);
      }

      const targetColumn = draft.columns.find(x => x.id === card.boardColumnId);
      if (!targetColumn) {
        return;
      }

      targetColumn.cards.push(card);
      sortCardsInColumns(draft.columns);
    });
  }

  function removeCard(cardId: number) {
    mutateBoard(draft => {
      for (const column of draft.columns) {
        const index = column.cards.findIndex(x => x.id === cardId);
        if (index < 0) {
          continue;
        }

        column.cards.splice(index, 1);
        return;
      }
    });
  }

  function getCardById(cardId: number | null) {
    if (!board.value || cardId === null) {
      return null;
    }

    for (const column of board.value.columns) {
      const card = column.cards.find(x => x.id === cardId);
      if (card) {
        return card;
      }
    }

    return null;
  }

  function getColumnById(columnId: number | null) {
    if (!board.value || columnId === null) {
      return null;
    }

    return board.value.columns.find(x => x.id === columnId) ?? null;
  }

  function removeTagFromCards(tagName: string) {
    const normalisedTagName = tagName.trim().toUpperCase();
    if (!normalisedTagName) {
      return;
    }

    mutateBoard(draft => {
      for (const column of draft.columns) {
        for (const card of column.cards) {
          card.tagNames = card.tagNames.filter(existingTagName => existingTagName.trim().toUpperCase() !== normalisedTagName);
        }
      }
    });
  }

  function mutateBoard(mutator: (draft: Board) => void) {
    if (!board.value) {
      return;
    }

    const draft = cloneBoard(board.value);
    mutator(draft);
    board.value = draft;
  }

  return {
    board,
    busy,
    isLoadingBoard,
    currentBoardId,
    initialize,
    dispose,
    createColumn,
    saveColumn,
    moveColumn,
    deleteColumn,
    createCard,
    saveCard,
    deleteCard,
    removeTagFromCards,
    getCardById,
    getColumnById,
    startDrag,
    dropCard
  };
});

function cloneBoard(source: Board): Board {
  return {
    ...source,
    columns: source.columns.map(column => ({
      ...column,
      cards: column.cards.map(card => ({ ...card, tagNames: [...card.tagNames] }))
    }))
  };
}

function sortCardsInColumns(columns: BoardColumn[]) {
  for (const column of columns) {
    column.cards.sort((a, b) => compareSortKey(a.sortKey, b.sortKey));
  }
}

function sortColumns(columns: BoardColumn[]) {
  columns.sort((a, b) => compareSortKey(a.sortKey, b.sortKey));
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

function resolvePositionAfterCardId(
  board: Board | null,
  movingCardId: number,
  targetColumnId: number,
  targetCardId: number | null
): number | null | undefined {
  if (!board) {
    return undefined;
  }

  const targetColumn = board.columns.find(x => x.id === targetColumnId);
  if (!targetColumn) {
    return undefined;
  }

  if (targetCardId === movingCardId) {
    return undefined;
  }

  const targetCards = targetColumn.cards.filter(x => x.id !== movingCardId);
  if (targetCardId === null) {
    return targetCards.length === 0 ? null : targetCards[targetCards.length - 1].id;
  }

  const targetIndex = targetCards.findIndex(x => x.id === targetCardId);
  if (targetIndex < 0) {
    return undefined;
  }

  return targetIndex === 0 ? null : targetCards[targetIndex - 1].id;
}
