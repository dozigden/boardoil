import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardCatalogueStore } from './boardCatalogueStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import { err, ok } from '../types/result';

const api = {
  getBoards: vi.fn(),
  createBoard: vi.fn(),
  importTasksMdBoard: vi.fn(),
  saveBoard: vi.fn(),
  deleteBoard: vi.fn()
};

vi.mock('../api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('boardCatalogueStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getBoards.mockResolvedValue(ok([]));
    api.createBoard.mockResolvedValue(ok(makeBoard(10, 'Roadmap')));
    api.importTasksMdBoard.mockResolvedValue(ok(makeBoard(11, 'tasks.example.net')));
    api.saveBoard.mockResolvedValue(ok(makeSummary(10, 'Roadmap')));
    api.deleteBoard.mockResolvedValue(ok(undefined));
  });

  it('imports tasksmd board and appends it to catalogue', async () => {
    const store = useBoardCatalogueStore();

    const imported = await store.importTasksMdBoard('https://tasks.example.net/');

    expect(api.importTasksMdBoard).toHaveBeenCalledWith('https://tasks.example.net/');
    expect(imported?.id).toBe(11);
    expect(store.boards.map(x => x.name)).toEqual(['tasks.example.net']);
  });

  it('reports API error when tasksmd import fails', async () => {
    const store = useBoardCatalogueStore();
    const feedback = useUiFeedbackStore();
    api.importTasksMdBoard.mockResolvedValueOnce(err({ kind: 'api', message: 'Import failed.' }));

    const imported = await store.importTasksMdBoard('https://tasks.example.net/');

    expect(imported).toBeNull();
    expect(feedback.errorMessage).toBe('Import failed.');
    expect(store.boards).toHaveLength(0);
  });
});

function makeBoard(id: number, name: string) {
  return {
    id,
    name,
    createdAtUtc: '2026-04-03T17:00:00Z',
    updatedAtUtc: '2026-04-03T17:00:00Z',
    currentUserRole: 'Owner',
    columns: []
  };
}

function makeSummary(id: number, name: string) {
  return {
    id,
    name,
    createdAtUtc: '2026-04-03T17:00:00Z',
    updatedAtUtc: '2026-04-03T17:00:00Z',
    currentUserRole: 'Owner'
  };
}
