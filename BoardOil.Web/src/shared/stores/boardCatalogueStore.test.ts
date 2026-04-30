import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardCatalogueStore } from './boardCatalogueStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import { err, ok } from '../types/result';

const api = {
  getBoards: vi.fn(),
  createBoard: vi.fn(),
  importBoardPackage: vi.fn(),
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
    api.importBoardPackage.mockResolvedValue(ok(makeBoard(12, 'Imported Board')));
    api.saveBoard.mockResolvedValue(ok(makeSummary(10, 'Roadmap')));
    api.deleteBoard.mockResolvedValue(ok(undefined));
  });

  it('imports board package and appends it to catalogue', async () => {
    const store = useBoardCatalogueStore();
    const file = new File(['zip-data'], 'board.boardoil.zip', { type: 'application/zip' });

    const imported = await store.importBoardPackage(file, 'Imported Name');

    expect(api.importBoardPackage).toHaveBeenCalledWith(file, 'Imported Name');
    expect(imported?.id).toBe(12);
    expect(store.boards.map(x => x.name)).toEqual(['Imported Board']);
  });

  it('reports API error when board package import fails', async () => {
    const store = useBoardCatalogueStore();
    const feedback = useUiFeedbackStore();
    const file = new File(['zip-data'], 'board.boardoil.zip', { type: 'application/zip' });
    api.importBoardPackage.mockResolvedValueOnce(err({ kind: 'api', message: 'Package import failed.' }));

    const imported = await store.importBoardPackage(file);

    expect(imported).toBeNull();
    expect(feedback.errorMessage).toBe('Package import failed.');
    expect(store.boards).toHaveLength(0);
  });
});

function makeBoard(id: number, name: string) {
  return {
    id,
    name,
    description: '',
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
    description: '',
    createdAtUtc: '2026-04-03T17:00:00Z',
    updatedAtUtc: '2026-04-03T17:00:00Z',
    currentUserRole: 'Owner'
  };
}
