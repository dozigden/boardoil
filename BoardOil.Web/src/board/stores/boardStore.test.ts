import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardStore } from './boardStore';
import { useCardTypeStore } from './cardTypeStore';
import { useTagStore } from './tagStore';
import { useSlickStore } from './slickStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AppError } from '../../shared/types/appError';
import type { Board, Card, Column } from '../../shared/types/boardTypes';
import { err, ok } from '../../shared/types/result';
import type { Result } from '../../shared/types/result';

const api = {
  getBoard: vi.fn(),
  createColumn: vi.fn(),
  saveColumn: vi.fn(),
  moveColumn: vi.fn(),
  deleteColumn: vi.fn()
};

const realtime = {
  connect: vi.fn(),
  disconnect: vi.fn()
};
type RealtimeHandlers = {
  onCardUpdated: (boardId: number, card: Card) => Promise<unknown> | unknown;
  onCardDeleted: (boardId: number, cardId: number) => Promise<unknown> | unknown;
  onResync: (boardId: number) => Promise<unknown> | unknown;
  onConnectionWarning?: (message: string) => Promise<unknown> | unknown;
  onConnectionRecovered?: () => Promise<unknown> | unknown;
};
let realtimeHandlers: RealtimeHandlers | null = null;
const systemInfoMessageStore = {
  setMessage: vi.fn(),
  load: vi.fn(async () => true)
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

vi.mock('../realtime/boardRealtime', () => ({
  createBoardRealtime: vi.fn(handlers => {
    realtimeHandlers = handlers;
    return realtime;
  })
}));

vi.mock('../../shared/stores/systemInfoMessageStore', () => ({
  useSystemInfoMessageStore: () => systemInfoMessageStore
}));

describe('boardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    realtimeHandlers = null;
    systemInfoMessageStore.setMessage.mockReset();
    systemInfoMessageStore.load.mockClear();
    api.getBoard.mockResolvedValue(ok(makeBoard()));
    realtime.connect.mockResolvedValue(undefined);
    realtime.disconnect.mockResolvedValue(undefined);
  });

  it('initializes board and connects realtime', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    expect(store.isLoadingBoard).toBe(false);

    await store.initialize(1);

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(api.getBoard).toHaveBeenCalledWith(1);
    expect(realtime.connect).toHaveBeenCalledWith(1);
    expect(store.isLoadingBoard).toBe(false);
    expect(store.board?.columns.length).toBe(2);
    expect(store.board?.columns[0].cards.map(x => x.id)).toEqual([101]);

    await realtimeHandlers!.onConnectionRecovered?.();

    expect(feedback.toastMessage).toBe('');
  });

  it('keeps board loaded and warns when realtime connect fails', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    realtime.connect.mockRejectedValueOnce(new Error('realtime failed'));

    const initialized = await store.initialize(1);

    expect(initialized).toBe(true);
    expect(store.board?.id).toBe(1);
    expect(store.currentBoardId).toBe(1);
    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(feedback.warningMessage).toBe('Realtime updates are unavailable. Data may be stale until reconnect.');
  });

  it('clears stale board state when requested board fails to load', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Board not found.' }));

    const initialized = await store.initialize(999);

    expect(initialized).toBe(false);
    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
  });

  it('ignores stale load response when board switches quickly', async () => {
    const store = useBoardStore();
    const delayed = deferred<Result<Board, AppError>>();
    api.getBoard
      .mockImplementationOnce(() => delayed.promise)
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    const firstLoad = store.initialize(1);
    const secondLoad = store.initialize(2);
    delayed.resolve(ok(makeBoard(1, 'Board 1')));
    await Promise.all([firstLoad, secondLoad]);

    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
    expect(store.board?.name).toBe('Board 2');
    expect(realtime.connect).toHaveBeenCalledTimes(1);
    expect(realtime.connect).toHaveBeenCalledWith(2);
  });

  it('ignores in-flight load response after dispose', async () => {
    const store = useBoardStore();
    const delayed = deferred<Result<Board, AppError>>();
    api.getBoard.mockImplementationOnce(() => delayed.promise);

    const pendingInit = store.initialize(1);
    expect(store.isLoadingBoard).toBe(true);
    await store.dispose();
    delayed.resolve(ok(makeBoard(1, 'Board 1')));
    const initialized = await pendingInit;

    expect(initialized).toBe(false);
    expect(store.isLoadingBoard).toBe(false);
    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
  });

  it('switches board context on sequential initialize calls', async () => {
    const store = useBoardStore();
    api.getBoard
      .mockResolvedValueOnce(ok(makeBoard(1, 'Board 1')))
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    const firstInitialized = await store.initialize(1);
    const secondInitialized = await store.initialize(2);

    expect(firstInitialized).toBe(true);
    expect(secondInitialized).toBe(true);
    expect(store.currentBoardId).toBe(2);
    expect(store.board?.id).toBe(2);
    expect(store.board?.name).toBe('Board 2');
    expect(realtime.connect).toHaveBeenNthCalledWith(1, 1);
    expect(realtime.connect).toHaveBeenNthCalledWith(2, 2);
  });

  it('ignores stale realtime events when two boards contain the same card id', async () => {
    const store = useBoardStore();
    api.getBoard
      .mockResolvedValueOnce(ok(makeBoard(1, 'Board 1')))
      .mockResolvedValueOnce(ok(makeBoard(2, 'Board 2')));

    await store.initialize(1);
    await store.initialize(2);
    expect(realtimeHandlers).not.toBeNull();
    const boardTwoCard = store.board!.columns[0].cards[0];
    const staleBoardOneCard = {
      ...boardTwoCard,
      title: 'Stale board one update'
    };

    await realtimeHandlers!.onCardUpdated(1, staleBoardOneCard);
    await realtimeHandlers!.onCardDeleted(1, boardTwoCard.id);

    expect(store.board!.columns[0].cards[0].title).toBe('Task A');
    expect(store.board!.columns[0].cards[0].id).toBe(101);

    await realtimeHandlers!.onCardUpdated(2, {
      ...boardTwoCard,
      title: 'Current board update'
    });

    expect(store.board!.columns[0].cards[0].title).toBe('Current board update');
  });

  it('creates a column incrementally without reloading board', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const created: Column = {
      id: 3,
      title: 'Done',
      sortKey: '00000000000000000030',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    api.createColumn.mockResolvedValue(ok(created));

    await store.createColumn({ title: 'Done' });

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'Doing', 'Done']);
  });

  it('saves a column incrementally using typed edit model payload', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const saved: Column = {
      id: 2,
      title: 'In Progress',
      sortKey: '00000000000000000020',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:02:00Z'
    };
    api.saveColumn.mockResolvedValue(ok(saved));

    await store.saveColumn(2, { title: 'In Progress' });

    expect(api.saveColumn).toHaveBeenCalledWith(1, 2, { title: 'In Progress' });
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'In Progress']);
  });

  it('reorders a column incrementally when updated sort key is returned', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const moved: Column = {
      id: 2,
      title: 'Doing',
      sortKey: '00000000000000000005',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:03:00Z'
    };
    api.moveColumn.mockResolvedValue(ok(moved));

    await store.moveColumn(2, null);

    expect(store.board?.columns.map(x => x.title)).toEqual(['Doing', 'Backlog']);
    expect(api.moveColumn).toHaveBeenCalledWith(1, 2, null);
  });

  it('sets feedback error when API returns failure', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    await store.initialize(1);

    const apiError: AppError = {
      kind: 'api',
      message: 'Column create failed.'
    };
    api.createColumn.mockResolvedValue(err(apiError));

    await store.createColumn({ title: 'Bad' });

    expect(feedback.errorMessage).toBe('Column create failed.');
  });

  it('reloads tags and slicks when realtime resync is requested', async () => {
    const store = useBoardStore();
    const cardTypeStore = useCardTypeStore();
    const tagStore = useTagStore();
    const slickStore = useSlickStore();
    const loadCardTypesSpy = vi.spyOn(cardTypeStore, 'loadCardTypes').mockResolvedValue(true);
    const loadTagsSpy = vi.spyOn(tagStore, 'loadTags').mockResolvedValue(true);
    const loadSlicksSpy = vi.spyOn(slickStore, 'loadSlicks').mockResolvedValue(true);

    await store.initialize(1);
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onResync(1);

    expect(api.getBoard).toHaveBeenCalledTimes(2);
    expect(loadCardTypesSpy).toHaveBeenCalledWith(1);
    expect(loadTagsSpy).toHaveBeenCalledWith(1);
    expect(loadSlicksSpy).toHaveBeenCalledWith(1);
  });

  it('does not reload board-scoped stores when realtime resync board reload fails', async () => {
    const store = useBoardStore();
    const cardTypeStore = useCardTypeStore();
    const tagStore = useTagStore();
    const slickStore = useSlickStore();
    const loadCardTypesSpy = vi.spyOn(cardTypeStore, 'loadCardTypes').mockResolvedValue(true);
    const loadTagsSpy = vi.spyOn(tagStore, 'loadTags').mockResolvedValue(true);
    const loadSlicksSpy = vi.spyOn(slickStore, 'loadSlicks').mockResolvedValue(true);

    await store.initialize(1);
    api.getBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Board not found.' }));
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onResync(1);

    expect(store.board).toBeNull();
    expect(store.currentBoardId).toBeNull();
    expect(loadCardTypesSpy).not.toHaveBeenCalled();
    expect(loadTagsSpy).not.toHaveBeenCalled();
    expect(loadSlicksSpy).not.toHaveBeenCalled();
    expect(systemInfoMessageStore.load).not.toHaveBeenCalled();
  });

  it('replaces the realtime reconnect warning with a success toast when recovery completes', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();

    await store.initialize(1);
    expect(realtimeHandlers).not.toBeNull();

    await realtimeHandlers!.onConnectionWarning?.('Realtime connection lost. Attempting to reconnect…');
    expect(feedback.warningMessage).toBe('Realtime connection lost. Attempting to reconnect…');

    await realtimeHandlers!.onConnectionRecovered?.();
    expect(feedback.warningMessage).toBe('');
    expect(feedback.toastMessage).toBe('Realtime updates restored.');
    expect(feedback.toastTone).toBe('success');

    await realtimeHandlers!.onConnectionWarning?.('Realtime connection lost. Attempting to reconnect…');
    expect(feedback.toastMessage).toBe('');
    expect(feedback.warningMessage).toBe('Realtime connection lost. Attempting to reconnect…');
  });
});

function makeBoard(id = 1, name = 'Board'): Board {
  return {
    id,
    name,
    description: '',
    slickCohesionModeEnabled: true,
    createdAtUtc: '2026-03-15T00:00:00Z',
    updatedAtUtc: '2026-03-15T00:00:00Z',
    columns: [
      {
        id: 1,
        title: 'Backlog',
        sortKey: '00000000000000000010',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: [
          {
            id: 101,
            boardColumnId: 1,
            cardTypeId: 1,
            cardTypeName: 'Story',
            cardTypeEmoji: null,
            title: 'Task A',
            description: 'Seed',
            externalUrl: null,
            sortKey: '00000000000000000001',
            tags: [],
            tagNames: [],
            cardCreatedUtc: '2026-03-15T00:00:00Z',
            cardUpdatedUtc: '2026-03-15T00:00:00Z'
          }
        ]
      },
      {
        id: 2,
        title: 'Doing',
        sortKey: '00000000000000000020',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: []
      }
    ]
  };
}

function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}
