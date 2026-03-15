import { beforeEach, describe, expect, it, vi } from 'vitest';
import { computed } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardStore } from './boardStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { AppError } from '../types/appError';
import type { Board, Card, Column } from '../types/boardTypes';
import { err, ok } from '../types/result';

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
  disconnect: vi.fn(),
  announceTyping: vi.fn(),
  stopTyping: vi.fn(),
  typingSummary: computed(() => () => [] as string[])
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
    realtime.stopTyping.mockResolvedValue(undefined);
  });

  it('initializes board and connects realtime once', async () => {
    const store = useBoardStore();
    await store.initialize();
    await store.initialize();

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(realtime.connect).toHaveBeenCalledTimes(1);
    expect(store.board?.columns.length).toBe(2);
  });

  it('creates a column incrementally without reloading board', async () => {
    const store = useBoardStore();
    await store.initialize();

    const created: Column = {
      id: 3,
      title: 'Done',
      position: 2,
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z'
    };
    api.createColumn.mockResolvedValue(ok(created));

    await store.createColumn('Done');

    expect(api.getBoard).toHaveBeenCalledTimes(1);
    expect(store.board?.columns.map(x => x.title)).toEqual(['Backlog', 'Doing', 'Done']);
  });

  it('reorders a column incrementally when updated position is returned', async () => {
    const store = useBoardStore();
    await store.initialize();

    const moved: Column = {
      id: 2,
      title: 'Doing',
      position: 0,
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:03:00Z'
    };
    api.moveColumn.mockResolvedValue(ok(moved));

    await store.moveColumn(2, 0);

    expect(store.board?.columns.map(x => x.title)).toEqual(['Doing', 'Backlog']);
    expect(store.board?.columns.map(x => x.position)).toEqual([0, 1]);
  });

  it('moves card across columns incrementally', async () => {
    const store = useBoardStore();
    await store.initialize();

    const moved: Card = {
      id: 101,
      boardColumnId: 2,
      title: 'Task A',
      description: 'Seed',
      position: 0,
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:01:00Z'
    };
    api.moveCard.mockResolvedValue(ok(moved));

    store.startDrag(101, 1);
    await store.dropCard(2, 0);

    expect(store.board?.columns[0].cards).toHaveLength(0);
    expect(store.board?.columns[1].cards[0].id).toBe(101);
    expect(api.getBoard).toHaveBeenCalledTimes(1);
  });

  it('saveCard updates card and stops typing signals', async () => {
    const store = useBoardStore();
    await store.initialize();

    const updated: Card = {
      id: 101,
      boardColumnId: 1,
      title: 'Task A+',
      description: 'Updated',
      position: 0,
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:02:00Z'
    };
    api.saveCard.mockResolvedValue(ok(updated));

    await store.saveCard(101, 'Task A+', 'Updated');

    expect(store.board?.columns[0].cards[0].title).toBe('Task A+');
    expect(realtime.stopTyping).toHaveBeenCalledWith(101, 'title');
    expect(realtime.stopTyping).toHaveBeenCalledWith(101, 'description');
  });

  it('finds card by id from board state', async () => {
    const store = useBoardStore();
    await store.initialize();

    expect(store.getCardById(101)?.title).toBe('Task A');
    expect(store.getCardById(999)).toBeNull();
    expect(store.getCardById(null)).toBeNull();
  });

  it('sets feedback error when API returns failure', async () => {
    const store = useBoardStore();
    const feedback = useUiFeedbackStore();
    await store.initialize();

    const apiError: AppError = {
      kind: 'api',
      message: 'Column create failed.'
    };
    api.createColumn.mockResolvedValue(err(apiError));

    await store.createColumn('Bad');

    expect(feedback.errorMessage).toBe('Column create failed.');
  });
});

function makeBoard(): Board {
  return {
    id: 1,
    name: 'Board',
    createdAtUtc: '2026-03-15T00:00:00Z',
    updatedAtUtc: '2026-03-15T00:00:00Z',
    columns: [
      {
        id: 1,
        title: 'Backlog',
        position: 0,
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: [
          {
            id: 101,
            boardColumnId: 1,
            title: 'Task A',
            description: 'Seed',
            position: 0,
            createdAtUtc: '2026-03-15T00:00:00Z',
            updatedAtUtc: '2026-03-15T00:00:00Z'
          }
        ]
      },
      {
        id: 2,
        title: 'Doing',
        position: 1,
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z',
        cards: []
      }
    ]
  };
}
