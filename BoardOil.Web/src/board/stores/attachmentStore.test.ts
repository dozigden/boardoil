import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAttachmentStore } from './attachmentStore';
import { err, ok } from '../../shared/types/result';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';

const api = { supportsAttachments: true, supportsAttachmentMutations: true, getAttachments: vi.fn(), getFirstAttachmentImagesByCard: vi.fn(), uploadAttachment: vi.fn(), deleteAttachment: vi.fn(), downloadAttachment: vi.fn() };
vi.mock('../../shared/api/boardApi', () => ({ createBoardApi: () => api }));
const attachment: CardAttachment = { id: 1, originalFileName: 'file.bin', byteLength: 3,
  contentType: 'application/octet-stream', createdAtUtc: '2026-09-11T12:00:00Z', createdByUserId: null, hasThumbnail: false };
const listing = (items: CardAttachment[] = []) => ok({ items, maxUploadByteLength: 10 });

describe('attachments', () => {
  afterEach(() => vi.unstubAllGlobals());

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
    api.supportsAttachments = true;
    api.supportsAttachmentMutations = true;
    api.getAttachments.mockResolvedValue(listing());
    api.getFirstAttachmentImagesByCard.mockResolvedValue(ok([]));
  });

  it('ignores a previous card load after navigation', async () => {
    const pending = deferred<ReturnType<typeof listing>>();
    api.getAttachments.mockReturnValueOnce(pending.promise);
    const store = useAttachmentStore();
    const previous = store.open(1, 1);
    await store.open(2, 1);
    pending.resolve(listing([attachment]));
    await previous;
    expect(store.items).toEqual([]);
  });

  it('reconciles realtime changes arriving during a stale list request', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    const pending = deferred<ReturnType<typeof listing>>();
    api.getAttachments.mockReturnValueOnce(pending.promise).mockResolvedValueOnce(listing([attachment]));
    const reload = store.reload();
    store.added(1, 1, attachment);
    pending.resolve(listing());
    await reload;
    expect(store.items).toEqual([attachment]);
    store.removed(1, 1, attachment.id);
    expect(store.items).toEqual([]);
  });

  it('advances the image revision only when attachment identities change', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    const emptyRevision = store.revision;

    await store.reload();
    expect(store.revision).toBe(emptyRevision);

    store.added(1, 1, attachment);
    const addedRevision = store.revision;
    expect(addedRevision).toBeGreaterThan(emptyRevision);

    api.getAttachments.mockResolvedValue(listing([attachment]));
    await store.reload();
    expect(store.revision).toBe(addedRevision);

    store.removed(1, 1, attachment.id);
    expect(store.revision).toBeGreaterThan(addedRevision);
  });

  it('uploads sequentially and removes failures from the queue with a warning', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.uploadAttachment.mockResolvedValueOnce(err({ kind: 'http', message: 'Storage unavailable' }))
      .mockResolvedValueOnce(ok({ ...attachment, id: 2, originalFileName: 'empty.bin', byteLength: 0 }));

    const uploaded = await store.upload([new File(['abc'], 'file.bin'), new File([''], 'empty.bin')]);

    expect(api.uploadAttachment).toHaveBeenCalledTimes(2);
    expect(uploaded).toEqual([{ ...attachment, id: 2, originalFileName: 'empty.bin', byteLength: 0 }]);
    expect(store.busy).toBe(false);
    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages).toEqual(['file.bin: Storage unavailable']);
  });

  it('loads read-only attachments without allowing uploads or deletions', async () => {
    api.supportsAttachmentMutations = false;
    api.getAttachments.mockResolvedValue(listing([attachment]));
    const store = useAttachmentStore();

    await store.open(1, 1);
    const uploaded = await store.upload([new File(['abc'], 'file.bin')]);
    const removed = await store.remove(attachment.id);

    expect(store.supported).toBe(true);
    expect(store.mutable).toBe(false);
    expect(store.items).toEqual([attachment]);
    expect(uploaded).toEqual([]);
    expect(removed).toBe(false);
    expect(api.uploadAttachment).not.toHaveBeenCalled();
    expect(api.deleteAttachment).not.toHaveBeenCalled();
  });

  it('reports when an attachment is successfully removed', async () => {
    api.getAttachments.mockResolvedValue(listing([attachment]));
    api.deleteAttachment.mockResolvedValue(ok(undefined));
    const store = useAttachmentStore();
    await store.open(1, 1);

    const removed = await store.remove(attachment.id);

    expect(removed).toBe(true);
    expect(store.items).toEqual([]);
  });

  it('reports upload progress with the matching file', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    const file = new File(['abc'], 'file.bin');
    api.uploadAttachment.mockImplementation(async (_boardId, _cardId, _file, onProgress) => {
      onProgress(37);
      return ok(attachment);
    });
    const onProgress = vi.fn();

    await store.upload([file], onProgress);

    expect(onProgress).toHaveBeenCalledWith(file, 37);
  });

  it('uploads the original image when browser thumbnail generation fails', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    vi.stubGlobal('createImageBitmap', vi.fn().mockRejectedValue(new Error('Decode failed')));
    api.uploadAttachment.mockResolvedValue(ok({ ...attachment, originalFileName: 'image.png' }));
    const file = new File(['x'], 'image.png', { type: 'image/png' });

    const uploaded = await store.upload([file]);

    expect(uploaded).toHaveLength(1);
    expect(api.uploadAttachment).toHaveBeenCalledWith(1, 1, file, expect.any(Function), expect.any(AbortSignal), null);
  });

  it('warns about an unconfirmed upload without keeping a failed entry or retrying it', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.uploadAttachment.mockResolvedValue(err({ kind: 'network', message: 'Connection lost' }));
    api.getAttachments.mockResolvedValue(listing([attachment]));

    await store.upload([new File(['abc'], 'file.bin')]);

    expect(store.items).toEqual([attachment]);
    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages[0]).toContain('Check the attachment list');
    expect(api.uploadAttachment).toHaveBeenCalledTimes(1);
    store.clearWarnings();
    expect(store.warningMessages).toEqual([]);
    expect(store.items).toEqual([attachment]);
  });

  it('refreshes the card thumbnail projection after an unconfirmed upload', async () => {
    const store = useAttachmentStore();
    const thumbnailStore = useCardAttachmentThumbnailStore();
    await thumbnailStore.loadBoard(1, true);
    await store.open(1, 1);
    const image = { ...attachment, originalFileName: 'saved.png' };
    api.uploadAttachment.mockResolvedValue(err({ kind: 'network', message: 'Connection lost' }));
    api.getAttachments.mockResolvedValue(listing([image]));
    api.getFirstAttachmentImagesByCard.mockResolvedValueOnce(ok([
      { cardId: 1, attachmentId: image.id, originalFileName: image.originalFileName, hasThumbnail: false }
    ]));

    await store.upload([new File(['abc'], image.originalFileName, { type: 'image/png' })]);

    expect(api.getFirstAttachmentImagesByCard).toHaveBeenLastCalledWith(1, [1]);
    expect(thumbnailStore.getForCard(1)?.attachmentId).toBe(image.id);
  });

  it('reports a duplicate filename in a warning without leaving a failed entry', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.uploadAttachment.mockResolvedValue(err({ kind: 'http', statusCode: 409, message: 'Filename already exists' }));

    await store.upload([new File(['abc'], 'file.bin')]);

    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages).toEqual(['file.bin: Filename already exists']);
  });

  it.each(['load', 'delete', 'download'])('uses a warning for %s errors', async action => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    const failure = err({ kind: 'http', statusCode: 403, message: 'Access denied' });
    api.getAttachments.mockResolvedValue(failure);
    api.deleteAttachment.mockResolvedValue(failure);
    api.downloadAttachment.mockResolvedValue(failure);

    if (action === 'load') { await store.reload(); }
    else if (action === 'delete') { await store.remove(1); }
    else { await store.download(1); }

    expect(store.warningMessages).toHaveLength(1);
    expect(store.warningMessages[0]).toContain('Access denied');
    expect(store.activeUploads).toHaveLength(0);
  });

  it('rejects oversized files locally and allows empty files', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.uploadAttachment.mockResolvedValue(ok(attachment));
    await store.upload([new File(['01234567890'], 'large'), new File([], 'empty')]);
    expect(api.uploadAttachment).toHaveBeenCalledTimes(1);
    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages).toEqual(['large: The file exceeds the upload limit of 10 B per file and was not uploaded.']);
  });

  it('does not create queue entries or send requests when every selected file is too large', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);

    await store.upload([new File(['01234567890'], 'large')]);

    expect(api.uploadAttachment).not.toHaveBeenCalled();
    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages).toEqual(['large: The file exceeds the upload limit of 10 B per file and was not uploaded.']);
    store.clearWarnings();
    expect(store.warningMessages).toEqual([]);
  });

  it('removes a rejected upload and warns if the server limit has changed', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.uploadAttachment.mockResolvedValue(err({ kind: 'http', statusCode: 413, message: 'Too large' }));

    await store.upload([new File(['abc'], 'file.bin')]);

    expect(store.activeUploads).toHaveLength(0);
    expect(store.warningMessages).toEqual(['file.bin: The file exceeds the upload limit of 10 B per file and was not uploaded.']);
  });

  it('aborts in-flight uploads and ignores their late responses on clear', async () => {
    const pending = deferred<ReturnType<typeof ok<CardAttachment>>>();
    api.uploadAttachment.mockReturnValue(pending.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    const uploading = store.upload([new File(['abc'], 'file.bin')]);
    const signal = api.uploadAttachment.mock.calls[0]![4] as AbortSignal;
    store.cardRemoved(1, 1);
    expect(signal.aborted).toBe(true);
    pending.resolve(ok(attachment));
    await uploading;
    expect(store.items).toEqual([]);
    expect(store.busy).toBe(false);
  });

  it('never sends uploads for archive or demo contexts', async () => {
    const store = useAttachmentStore();
    await store.open(1, 1, true);
    await store.upload([new File([], 'empty')]);
    expect(api.uploadAttachment).not.toHaveBeenCalled();
    setActivePinia(createPinia());
    api.supportsAttachments = false;
    api.getAttachments.mockClear();
    await useAttachmentStore().open(1, 1);
    expect(api.getAttachments).not.toHaveBeenCalled();
  });
});

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
}
