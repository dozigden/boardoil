import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardCatalogueStore } from './boardCatalogueStore';
import { useUiFeedbackStore } from './uiFeedbackStore';
import { err, ok } from '../types/result';

const api = {
  getBoards: vi.fn(),
  createBoard: vi.fn(),
  cloneBoard: vi.fn(),
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
    api.cloneBoard.mockResolvedValue(ok(makeBoard(11, 'Cloned Board')));
    api.importBoardPackage.mockResolvedValue(ok(makeBoard(12, 'Imported Board')));
    api.saveBoard.mockResolvedValue(ok(makeSummary(10, 'Roadmap')));
    api.deleteBoard.mockResolvedValue(ok(undefined));
  });

  it('loads boards in ID order without changing the response array', async () => {
    const store = useBoardCatalogueStore();
    const response = [makeSummary(20, 'Later'), makeSummary(2, 'Earlier')];
    api.getBoards.mockResolvedValueOnce(ok(response));

    expect(await store.loadBoards()).toBe(true);
    expect(store.boards.map(board => board.id)).toEqual([2, 20]);
    expect(response.map(board => board.id)).toEqual([20, 2]);
  });

  it('lets a dialog handle board lookup failure without also publishing a page error', async () => {
    const store = useBoardCatalogueStore();
    const feedback = useUiFeedbackStore();
    const publish = vi.spyOn(feedback, 'setError');
    api.getBoards.mockResolvedValue(err({ kind: 'api', message: 'Boards unavailable' }));
    expect(await store.loadBoards({ suppressError: true })).toBe(false);
    expect(publish).not.toHaveBeenCalled();
    expect(await store.loadBoards()).toBe(false);
    expect(publish).toHaveBeenCalledWith('Boards unavailable', 'boardCatalogue');
  });

  it.each(['create', 'clone', 'import', 'save'] as const)('%s upserts a board already present in the catalogue', async operation => {
    const store = useBoardCatalogueStore();
    const original = makeSummary(10, 'Original');
    const other = makeSummary(20, 'Unrelated');
    api.getBoards.mockResolvedValueOnce(ok([other, original]));
    await store.loadBoards();
    const previous = store.boards;
    const updated = makeBoard(10, 'Updated');
    switch (operation) {
      case 'create':
        api.createBoard.mockResolvedValueOnce(ok(updated));
        await store.createBoard('Updated');
        break;
      case 'clone':
        api.cloneBoard.mockResolvedValueOnce(ok(updated));
        await store.cloneBoard(1, 'Updated');
        break;
      case 'import':
        api.importBoardPackage.mockResolvedValueOnce(ok(updated));
        await store.importBoardPackage(new File(['data'], 'board.zip'));
        break;
      case 'save':
        api.saveBoard.mockResolvedValueOnce(ok(makeSummary(10, 'Updated')));
        await store.saveBoard(10, updated);
        break;
    }

    expect(store.boards).toEqual([makeSummary(10, 'Updated'), other]);
    expect(previous).toEqual([original, other]);
    expect(store.boards[0]).not.toHaveProperty('columns');
  });

  it('adds an authoritative saved board missing from the catalogue', async () => {
    const store = useBoardCatalogueStore();
    const saved = await store.saveBoard(10, makeSummary(10, 'Roadmap'));
    expect(saved).toEqual(ok(makeSummary(10, 'Roadmap')));
    expect(store.boards).toEqual([makeSummary(10, 'Roadmap')]);
  });

  it.each(['success', 'failure'] as const)('handles board deletion %s without changing unrelated entries', async outcome => {
    const store = useBoardCatalogueStore();
    const first = makeSummary(10, 'First');
    const second = makeSummary(20, 'Second');
    api.getBoards.mockResolvedValueOnce(ok([first, second]));
    await store.loadBoards();
    api.deleteBoard.mockResolvedValueOnce(outcome === 'success'
      ? ok(undefined)
      : err({ kind: 'api', message: 'Delete failed' }));

    expect((await store.deleteBoard(10)).ok).toBe(outcome === 'success');
    expect(store.boards).toEqual(outcome === 'success' ? [second] : [first, second]);
    expect(store.busy).toBe(false);
  });

  it('imports board package and appends it to catalogue', async () => {
    const store = useBoardCatalogueStore();
    const file = new File(['zip-data'], 'board.boardoil.zip', { type: 'application/zip' });

    const imported = await store.importBoardPackage(file, 'Imported Name');

    expect(api.importBoardPackage).toHaveBeenCalledWith(file, 'Imported Name');
    expect(imported).toEqual(ok(makeBoard(12, 'Imported Board')));
    expect(store.boards.map(x => x.name)).toEqual(['Imported Board']);
  });

  it('clones a board and appends it to catalogue', async () => {
    const store = useBoardCatalogueStore();

    const cloned = await store.cloneBoard(4, 'Cloned Board');

    expect(api.cloneBoard).toHaveBeenCalledWith(4, 'Cloned Board');
    expect(cloned).toEqual(ok(makeBoard(11, 'Cloned Board')));
    expect(store.boards.map(x => x.name)).toEqual(['Cloned Board']);
  });

  it.each(['create', 'clone', 'import', 'save', 'delete'] as const)('returns %s rejection intact without a duplicate page error', async operation => {
    const store = useBoardCatalogueStore();
    const feedback = useUiFeedbackStore();
    const publish = vi.spyOn(feedback, 'setError');
    const rejection = err({ kind: 'api' as const, message: 'Creation failed.', validationErrors: { name: ['Name rejected'] } });
    const file = new File(['zip-data'], 'board.zip');
    let result;
    switch (operation) {
      case 'create': api.createBoard.mockResolvedValueOnce(rejection); result = await store.createBoard('Name'); break;
      case 'clone': api.cloneBoard.mockResolvedValueOnce(rejection); result = await store.cloneBoard(4, 'Name'); break;
      case 'import': api.importBoardPackage.mockResolvedValueOnce(rejection); result = await store.importBoardPackage(file); break;
      case 'save': api.saveBoard.mockResolvedValueOnce(rejection); result = await store.saveBoard(1, makeSummary(1, 'Name')); break;
      case 'delete': api.deleteBoard.mockResolvedValueOnce(rejection); result = await store.deleteBoard(1); break;
    }
    expect(result).toBe(rejection);
    expect(publish).not.toHaveBeenCalled();
    expect(store.boards).toHaveLength(0);
    expect(store.busy).toBe(false);
  });
});

function makeBoard(id: number, name: string) {
  return {
    id,
    name,
    description: '',
    slickCohesionModeEnabled: true,
    cardAttachmentThumbnailsEnabled: true,
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
    slickCohesionModeEnabled: true,
    cardAttachmentThumbnailsEnabled: true,
    createdAtUtc: '2026-04-03T17:00:00Z',
    updatedAtUtc: '2026-04-03T17:00:00Z',
    currentUserRole: 'Owner'
  };
}
