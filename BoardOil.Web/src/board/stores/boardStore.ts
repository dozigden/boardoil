import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { sortBoard } from '../mappers/sortBoard';
import { createBoardRealtime } from '../realtime/boardRealtime';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useCardStore } from './cardStore';
import { useCardTypeStore } from './cardTypeStore';
import { useCommentStore } from './commentStore';
import { useTagStore } from './tagStore';
import { useSlickStore } from './slickStore';
import { useAttachmentStore } from './attachmentStore';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import { useSystemInfoMessageStore } from '../../shared/stores/systemInfoMessageStore';
import type {
  Board,
  BoardSummary,
  Card,
  CardComment,
  Column,
  ColumnCreateModel,
  ColumnEditModel
} from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

type BoardShell = Omit<Board, 'columns'> & {
  columns: Column[];
};

export const useBoardStore = defineStore('board', () => {
  const boardShell = ref<BoardShell | null>(null);
  const busy = ref(false);
  const isLoadingBoard = ref(false);
  const currentBoardId = computed(() => boardShell.value?.id ?? null);
  const feedback = useUiFeedbackStore();
  const cardStore = useCardStore();
  const cardTypeStore = useCardTypeStore();
  const commentStore = useCommentStore();
  const tagStore = useTagStore();
  const slickStore = useSlickStore();
  const attachmentStore = useAttachmentStore();
  const cardAttachmentThumbnailStore = useCardAttachmentThumbnailStore();
  const api = createBoardApi();
  const systemInfoMessageStore = useSystemInfoMessageStore();
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

  function forCurrentBoard<Args extends unknown[]>(
    handler: (boardId: number, ...args: Args) => void | Promise<void>
  ) {
    return (boardId: number, ...args: Args) => {
      if (currentBoardId.value !== boardId) {
        return;
      }

      return handler(boardId, ...args);
    };
  }

  const upsertRealtimeColumn = forCurrentBoard((_boardId, column: Column) => upsertColumn(column));
  const upsertRealtimeCard = forCurrentBoard((_boardId, card: Card) => cardStore.upsertCard(card));

  const realtime = createBoardRealtime({
    onColumnCreated: upsertRealtimeColumn,
    onColumnUpdated: upsertRealtimeColumn,
    onColumnDeleted: forCurrentBoard((_boardId, columnId: number) => removeColumn(columnId)),
    onCardCreated: forCurrentBoard((_boardId, card: Card) => cardStore.applyCreatedCard(card)),
    onCardUpdated: upsertRealtimeCard,
    onCardDeleted: forCurrentBoard((_boardId, cardId: number) => cardStore.removeCard(cardId)),
    onCardMoved: upsertRealtimeCard,
    onCommentCreated: forCurrentBoard((_boardId, comment: CardComment) => commentStore.upsertCardComment(comment)),
    onAttachmentAdded: forCurrentBoard(attachmentStore.added),
    onAttachmentDeleted: forCurrentBoard(attachmentStore.removed),
    onSystemInfoMessageUpdated: systemInfoMessageStore.setMessage,
    onConnectionWarning: message => {
      feedback.clearToast();
      feedback.setWarning(message);
    },
    onConnectionRecovered: () => {
      const wasRecovering = feedback.warningMessage !== '';
      feedback.clearWarning();
      if (wasRecovering) {
        feedback.showToast('Realtime updates restored.');
      }
    },
    onResync: resyncBoardFromRealtime
  });
  let loadRequestVersion = 0;
  let initializeRequestVersion = 0;

  async function resyncBoardFromRealtime(boardId: number) {
    if (currentBoardId.value !== boardId) {
      return;
    }

    const loaded = await loadBoard(boardId);
    if (!loaded) {
      return;
    }

    await loadBoardCatalogues(boardId);
    if (currentBoardId.value !== boardId) {
      return;
    }

    await attachmentStore.reload();
    await systemInfoMessageStore.load(true);
  }

  async function loadBoardCatalogues(boardId: number) {
    await Promise.all([
      cardTypeStore.loadCardTypes(boardId),
      tagStore.loadTags(boardId),
      slickStore.loadSlicks(boardId)
    ]);
  }

  async function initialize(boardId: number) {
    const requestVersion = ++initializeRequestVersion;
    isLoadingBoard.value = true;
    try {
      const loaded = await loadBoard(boardId);
      if (!loaded) {
        return false;
      }

      if (requestVersion !== initializeRequestVersion) {
        return false;
      }

      await loadBoardCatalogues(boardId);
      if (requestVersion !== initializeRequestVersion) {
        return false;
      }

      try {
        await realtime.connect(boardId);
        if (requestVersion !== initializeRequestVersion) {
          await realtime.disconnect();
          return false;
        }

        return true;
      } catch {
        if (requestVersion !== initializeRequestVersion) {
          return false;
        }

        feedback.setWarning('Realtime updates are unavailable. Data may be stale until reconnect.');
        await realtime.disconnect();
        return true;
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
    clearBoardContext();
    isLoadingBoard.value = false;
  }

  async function loadBoard(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    const result = await api.getBoard(boardId);
    if (requestVersion !== loadRequestVersion) {
      return false;
    }

    if (!result.ok) {
      clearBoardContext();
      reportError(result.error);
      return false;
    }

    const sortedBoard = sortBoard(result.data);
    void cardAttachmentThumbnailStore.loadBoard(boardId, sortedBoard.cardAttachmentThumbnailsEnabled);
    boardShell.value = stripBoardCards(sortedBoard);
    cardStore.replaceBoardCards(boardId, sortedBoard.columns);
    commentStore.dispose();
    feedback.clearError();
    return true;
  }

  async function createColumn(model: ColumnCreateModel) {
    model.title = model.title.trim();
    if (!model.title) {
      return;
    }

    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.createColumn(boardId, model));
    if (!result.ok) {
      return;
    }

    upsertColumn(result.data);
  }

  async function saveColumn(columnId: number, model: ColumnEditModel) {
    const boardId = getCurrentBoardIdOrReport();
    if (boardId === null) {
      return;
    }

    const result = await runBusy(() => api.saveColumn(boardId, columnId, model));
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

  function applyBoardSummaryUpdate(summary: Pick<BoardSummary,
    'id' | 'name' | 'description' | 'slickCohesionModeEnabled' | 'cardAttachmentThumbnailsEnabled' | 'updatedAtUtc'>) {
    const thumbnailSettingChanged = boardShell.value?.id === summary.id
      && boardShell.value.cardAttachmentThumbnailsEnabled !== summary.cardAttachmentThumbnailsEnabled;
    mutateBoardShell(draft => {
      if (draft.id !== summary.id) {
        return;
      }

      draft.name = summary.name;
      draft.description = summary.description;
      draft.slickCohesionModeEnabled = summary.slickCohesionModeEnabled;
      draft.cardAttachmentThumbnailsEnabled = summary.cardAttachmentThumbnailsEnabled;
      draft.updatedAtUtc = summary.updatedAtUtc;
    });
    if (thumbnailSettingChanged) {
      void cardAttachmentThumbnailStore.loadBoard(summary.id, summary.cardAttachmentThumbnailsEnabled);
    }
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

  function clearBoardContext() {
    attachmentStore.clear();
    cardAttachmentThumbnailStore.clear();
    boardShell.value = null;
    cardStore.dispose();
    commentStore.dispose();
    feedback.clearWarning();
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
