import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardStore } from './boardStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { AppError } from '../types/appError';
import type { Board, Card, Column } from '../types/boardTypes';
import { err, ok } from '../types/result';
import type { Result } from '../types/result';

const api = {
  getBoard: vi.fn(),
  createColumn: vi.fn(),
  saveColumn: vi.fn(),
  moveColumn: vi.fn(),
  deleteColumn: vi.fn(),
  createCard: vi.fn(),
  saveCard: vi.fn(),
  moveCard: vi.fn(),
  deleteCard: vi.fn()
};

const realtime = {
  connect: vi.fn(),
  disconnect: vi.fn()
};

vi.mock('../api/boardApi', () => ({
  createBoardApi: () => api
}));

vi.mock('../realtime/boardRealtime', () => ({
  createBoardRealtime: vi.fn(() => realtime)
}));

describe('boardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getBoard.mockResolvedValue(ok(makeBoard()));
    realtime.connect.mockResolvedValue(undefined);
    realtime.disconnect.mockResolvedValue(undefined);
  });

  it('initializes board and connects realtime', async () => {
    const store = useBoardStore();
    expect(store.isLoadingBoard).toBe(false);
    await store.initialize(1);

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(api.getBoard).toHaveBeenCalledWith(1);
    expect(realtime.connect).toHaveBeenCalledWith(1);
    expect(store.isLoadingBoard).toBe(false);
    expect(store.board?.columns.length).toBe(2);
  });

  it('fails initialization and disconnects when realtime connect fails', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    realtime.connect.mockRejectedValueOnce(new Error('realtime failed'));

    const initialized = await store.initialize(1);

    expect(initialized).toBe(false);
    expect(store.board?.id).toBe(1);
    expect(realtime.disconnect).toHaveBeenCalledTimes(1);
    expect(feedback.errorMessage).toBe('Realtime connection failed.');
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

    await store.createColumn('Done');

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'Doing', 'Done']);
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

  it('moves card across columns incrementally', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      title: 'Task A',
      description: 'Seed',
      sortKey: '00000000000000000001',
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, null);

    expect(store.board?.columns[0].cards).toHaveLength(0);
    expect(store.board?.columns[1].cards[0].id).toBe(101);
    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, null);
    expect(api.getBoard).toHaveBeenCalledTimes(1);
  });

  it('translates drop-before-card into predecessor anchor', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    store.board = {
      ...store.board!,
      columns: [
        {
          ...store.board!.columns[0],
          cards: [store.board!.columns[0].cards[0]]
        },
        {
          ...store.board!.columns[1],
          cards: [
            {
              id: 201,
              boardColumnId: 2,
              title: 'Task B',
              description: 'Seed',
              sortKey: '00000000000000000010',
              tagNames: [],
              createdAtUtc: '2026-03-15T00:00:00Z',
              updatedAtUtc: '2026-03-15T00:00:00Z'
            },
            {
              id: 202,
              boardColumnId: 2,
              title: 'Task C',
              description: 'Seed',
              sortKey: '00000000000000000020',
              tagNames: [],
              createdAtUtc: '2026-03-15T00:00:00Z',
              updatedAtUtc: '2026-03-15T00:00:00Z'
            }
          ]
        }
      ]
    };

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      title: 'Task A',
      description: 'Seed',
      sortKey: '00000000000000000015',
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, 202);

    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, 201);
  });

  it('uses null anchor when dropping before first card', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    store.board = {
      ...store.board!,
      columns: [
        {
          ...store.board!.columns[0],
          cards: [store.board!.columns[0].cards[0]]
        },
        {
          ...store.board!.columns[1],
          cards: [
            {
              id: 201,
              boardColumnId: 2,
              title: 'Task B',
              description: 'Seed',
              sortKey: '00000000000000000010',
              tagNames: [],
              createdAtUtc: '2026-03-15T00:00:00Z',
              updatedAtUtc: '2026-03-15T00:00:00Z'
            }
          ]
        }
      ]
    };

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      title: 'Task A',
      description: 'Seed',
      sortKey: '00000000000000000005',
      tagNames: [],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, 201);

    expect(api.moveCard).toHaveBeenCalledWith(1, 101, 2, null);
  });

  it('saveCard updates card', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    const updated: Card = {
      id: 101,
      boardColumnId: 1,
      title: 'Task A+',
      description: 'Updated',
      sortKey: '00000000000000000001',
      tagNames: ['Bug'],
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:02:00Z'
    };
    api.saveCard.mockResolvedValue(ok(updated));

    await store.saveCard(101, 'Task A+', 'Updated', ['Bug']);

    expect(store.board?.columns[0].cards[0].title).toBe('Task A+');
    expect(store.board?.columns[0].cards[0].tagNames).toEqual(['Bug']);
  });

  it('removeTagFromCards strips matching tags case-insensitively', async () => {
    const store = useBoardStore();
    await store.initialize(1);
    store.board = {
      ...store.board!,
      columns: [
        {
          ...store.board!.columns[0],
          cards: [
            {
              ...store.board!.columns[0].cards[0],
              tagNames: ['Bug', 'urgent']
            }
          ]
        },
        ...store.board!.columns.slice(1)
      ]
    };

    store.removeTagFromCards(' bug ');

    expect(store.board?.columns[0].cards[0].tagNames).toEqual(['urgent']);
  });

  it('finds card by id from board state', async () => {
    const store = useBoardStore();
    await store.initialize(1);

    expect(store.getCardById(101)?.title).toBe('Task A');
    expect(store.getCardById(999)).toBeNull();
    expect(store.getCardById(null)).toBeNull();
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

    await store.createColumn('Bad');

    expect(feedback.errorMessage).toBe('Column create failed.');
  });

});

function makeBoard(id = 1, name = 'Board'): Board {
  return {
    id,
    name,
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
            title: 'Task A',
            description: 'Seed',
            sortKey: '00000000000000000001',
            tagNames: [],
            createdAtUtc: '2026-03-15T00:00:00Z',
            updatedAtUtc: '2026-03-15T00:00:00Z'
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
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}
