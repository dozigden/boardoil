import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardAttachmentInventoryStore } from './boardAttachmentInventoryStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';
import { defaultBoardAttachmentInventoryQuery, type BoardAttachmentInventory } from '../../shared/types/attachmentTypes';

const api = { getBoardAttachments: vi.fn(), downloadAttachment: vi.fn() };
vi.mock('../../shared/api/boardApi', () => ({ createBoardApi: () => api }));
const empty: BoardAttachmentInventory = { items: [], totalCount: 0, totalByteLength: 0, matchingCount: 0, offset: 0, limit: 50 };

describe('boardAttachmentInventoryStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it('does not display attachments from a previous board when an old request completes', async () => {
    let resolveFirst!: (value: ReturnType<typeof ok<BoardAttachmentInventory>>) => void;
    api.getBoardAttachments.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; }));
    api.getBoardAttachments.mockResolvedValueOnce(ok(empty));
    const store = useBoardAttachmentInventoryStore();
    const first = store.load(1);
    await store.load(2);

    resolveFirst(ok({ ...empty, totalCount: 5, totalByteLength: 123 }));
    await first;

    expect(store.inventory).toEqual(empty);
    expect(store.loading).toBe(false);
  });

  it('clears previous totals and reports a refresh failure instead of showing an empty board', async () => {
    api.getBoardAttachments.mockResolvedValueOnce(ok(empty));
    api.getBoardAttachments.mockResolvedValueOnce(err({ kind: 'api', message: 'Permission denied' }));
    const store = useBoardAttachmentInventoryStore();
    await store.load(1);

    await store.load(1);

    expect(store.inventory).toBeNull();
    expect(store.error).toBe('Permission denied');
    expect(useUiFeedbackStore().errorMessage).toBe('Permission denied');
    expect(store.loading).toBe(false);
  });

  it('ignores a pending inventory response after leaving the page', async () => {
    let resolve!: (value: ReturnType<typeof ok<BoardAttachmentInventory>>) => void;
    api.getBoardAttachments.mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    const store = useBoardAttachmentInventoryStore();
    const pending = store.load(1);
    store.clear();

    resolve(ok(empty));
    await pending;

    expect(store.inventory).toBeNull();
    expect(store.loading).toBe(false);
  });

  it('reports download failures through shared feedback', async () => {
    api.getBoardAttachments.mockResolvedValueOnce(ok(empty));
    api.downloadAttachment.mockResolvedValueOnce(err({ kind: 'api', message: 'File missing' }));
    const store = useBoardAttachmentInventoryStore();
    await store.load(12);

    await store.download(3);

    expect(api.downloadAttachment).toHaveBeenCalledWith(12, 3);
    expect(useUiFeedbackStore().errorMessage).toBe('Attachment could not be downloaded: File missing');
  });

  it('passes paging and filters to the API and ignores an older sort response on the same board', async () => {
    let resolveFirst!: (value: ReturnType<typeof ok<BoardAttachmentInventory>>) => void;
    api.getBoardAttachments.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; }));
    api.getBoardAttachments.mockResolvedValueOnce(ok(empty));
    const store = useBoardAttachmentInventoryStore();
    const first = store.load(1, { ...defaultBoardAttachmentInventoryQuery, offset: 50 });
    const query = { ...defaultBoardAttachmentInventoryQuery, sort: 'size' as const, state: 'archived' as const };

    await store.load(1, query);
    resolveFirst(ok({ ...empty, offset: 50, matchingCount: 100 }));
    await first;

    expect(api.getBoardAttachments).toHaveBeenLastCalledWith(1, query);
    expect(store.inventory).toEqual(empty);
  });
});
