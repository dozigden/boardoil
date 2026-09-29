import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAttachmentStore } from './attachmentStore';
import { err, ok } from '../../shared/types/result';
import type { CardAttachment } from '../../shared/types/attachmentTypes';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import * as attachmentThumbnails from '../utils/attachmentThumbnails';

const api = { supportsAttachments: true, supportsAttachmentMutations: true, getAttachments: vi.fn(), getCardThumbnails: vi.fn(), uploadAttachment: vi.fn(), deleteAttachment: vi.fn(), downloadAttachment: vi.fn() };
vi.mock('../../shared/api/boardApi', () => ({ createBoardApi: () => api }));
const attachment: CardAttachment = { id: 1, originalFileName: 'file.bin', byteLength: 3,
  contentType: 'application/octet-stream', createdAtUtc: '2026-09-11T12:00:00Z', createdByUserId: null, hasThumbnail: false };
const listing = (items: CardAttachment[] = []) => ok({ items, maxUploadByteLength: 10 });

describe('attachments', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
    api.supportsAttachments = true;
    api.supportsAttachmentMutations = true;
    api.getAttachments.mockResolvedValue(listing());
    api.getCardThumbnails.mockResolvedValue(ok([]));
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
    await store.removed(1, 1, attachment.id);
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

    await store.removed(1, 1, attachment.id);
    expect(store.revision).toBeGreaterThan(addedRevision);
  });

  it.each([
    { context: 'no card open', openCardId: null },
    { context: 'another card open', openCardId: 3 },
    { context: 'the affected card open', openCardId: 2 }
  ])('updates attachment thumbnails with $context', async ({ openCardId }) => {
    const store = useAttachmentStore();
    const thumbnailStore = useCardAttachmentThumbnailStore();
    await thumbnailStore.loadBoard(1, true);
    if (openCardId !== null) {
      api.getAttachments.mockResolvedValue(listing([attachment]));
      await store.open(1, openCardId);
    }
    const initialItems = [...store.items];
    const image = { ...attachment, id: 2, originalFileName: 'image.png', contentType: 'image/png' };

    store.added(1, 2, image);

    expect(thumbnailStore.getForCard(2)?.attachmentId).toBe(image.id);
    if (openCardId === 2) {
      expect(store.items).toEqual([attachment, image]);
    } else {
      expect(store.items).toEqual(initialItems);
    }

    await store.removed(1, 2, image.id);

    expect(store.items).toEqual(initialItems);
    expect(thumbnailStore.getForCard(2)).toBeNull();
    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(1, [2]);
  });

  it('updates the attachment list and thumbnail after local image uploads and deletions', async () => {
    const store = useAttachmentStore();
    const thumbnailStore = useCardAttachmentThumbnailStore();
    await thumbnailStore.loadBoard(1, true);
    await store.open(1, 1);
    const image = { ...attachment, originalFileName: 'image.png', contentType: 'image/png' };
    vi.stubGlobal('createImageBitmap', vi.fn().mockRejectedValue(new Error('Decode failed')));
    api.uploadAttachment.mockResolvedValue(ok(image));
    api.getAttachments.mockResolvedValue(listing([image]));

    await store.upload([new File(['abc'], image.originalFileName, { type: image.contentType })]);

    expect(store.items).toEqual([image]);
    expect(thumbnailStore.getForCard(1)?.attachmentId).toBe(image.id);

    api.deleteAttachment.mockResolvedValue(ok(undefined));
    expect(await store.remove(image.id)).toBe(true);

    expect(store.items).toEqual([]);
    expect(thumbnailStore.getForCard(1)).toBeNull();
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

  it('keeps overlapping upload results and progress with their callers', async () => {
    const firstResponse = deferred<ReturnType<typeof ok<CardAttachment>>>();
    const secondResponse = deferred<ReturnType<typeof ok<CardAttachment>>>();
    const secondAttachment = { ...attachment, id: 2, originalFileName: 'second.bin' };
    api.uploadAttachment.mockReturnValueOnce(firstResponse.promise).mockReturnValueOnce(secondResponse.promise);
    api.getAttachments.mockResolvedValueOnce(listing()).mockResolvedValueOnce(listing([attachment]))
      .mockResolvedValueOnce(listing([attachment, secondAttachment]));
    const store = useAttachmentStore();
    await store.open(1, 1);
    const firstFile = new File(['a'], 'first.bin');
    const secondFile = new File(['b'], 'second.bin');
    const firstProgress = vi.fn();
    const secondProgress = vi.fn();
    const first = store.upload([firstFile], firstProgress);
    const secondSettled = vi.fn();
    const second = store.upload([secondFile], secondProgress).then(result => {
      secondSettled();
      return result;
    });

    await Promise.resolve();
    expect(secondSettled).not.toHaveBeenCalled();
    expect(api.uploadAttachment).toHaveBeenCalledTimes(1);
    const firstCallback = api.uploadAttachment.mock.calls[0]![3] as (percent: number) => void;
    firstCallback(25);
    expect(firstProgress).toHaveBeenCalledWith(firstFile, 25);
    expect(secondProgress).not.toHaveBeenCalled();

    firstResponse.resolve(ok(attachment));
    expect(await first).toEqual([attachment]);
    expect(secondSettled).not.toHaveBeenCalled();
    expect(api.uploadAttachment).toHaveBeenCalledTimes(2);
    const secondCallback = api.uploadAttachment.mock.calls[1]![3] as (percent: number) => void;
    secondCallback(75);
    expect(secondProgress).toHaveBeenCalledWith(secondFile, 75);
    expect(firstProgress).toHaveBeenCalledTimes(1);

    secondResponse.resolve(ok(secondAttachment));
    expect(await second).toEqual([secondAttachment]);
    expect(store.items).toEqual([attachment, secondAttachment]);
    expect(store.activeUploads).toEqual([]);
    expect(store.busy).toBe(false);
  });

  it('drains uploads queued while the preceding batch is refreshing', async () => {
    const refresh = deferred<ReturnType<typeof listing>>();
    const secondAttachment = { ...attachment, id: 2, originalFileName: 'second.bin' };
    api.uploadAttachment.mockResolvedValueOnce(ok(attachment)).mockResolvedValueOnce(ok(secondAttachment));
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.getAttachments.mockReturnValueOnce(refresh.promise).mockResolvedValueOnce(listing([attachment, secondAttachment]));
    const first = store.upload([new File(['a'], 'first.bin')]);
    await vi.waitFor(() => expect(api.getAttachments).toHaveBeenCalledTimes(2));
    const second = store.upload([new File(['b'], 'second.bin')]);
    expect(store.busy).toBe(true);
    expect(api.uploadAttachment).toHaveBeenCalledTimes(1);

    refresh.resolve(listing([attachment]));

    expect(await first).toEqual([attachment]);
    expect(await second).toEqual([secondAttachment]);
    expect(store.items).toEqual([attachment, secondAttachment]);
    expect(store.activeUploads).toEqual([]);
    expect(store.busy).toBe(false);
  });

  it.each(['cancel', 'clear'] as const)('stops uploads safely during thumbnail generation on %s', async action => {
    const thumbnail = deferred<Blob>();
    vi.spyOn(attachmentThumbnails, 'createAttachmentThumbnail').mockReturnValueOnce(thumbnail.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    const uploading = store.upload([new File(['x'], 'image.png')]);

    store[action]();
    thumbnail.resolve(new Blob(['thumbnail']));

    expect(await uploading).toEqual([]);
    expect(api.uploadAttachment).not.toHaveBeenCalled();
    expect(store.activeUploads).toEqual([]);
    expect(store.busy).toBe(false);
  });

  it('keeps a new upload active when cancelled thumbnail generation finishes', async () => {
    const thumbnail = deferred<Blob>();
    const newResponse = deferred<ReturnType<typeof ok<CardAttachment>>>();
    vi.spyOn(attachmentThumbnails, 'createAttachmentThumbnail').mockReturnValueOnce(thumbnail.promise);
    api.uploadAttachment.mockReturnValueOnce(newResponse.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    const oldProgress = vi.fn();
    const oldUpload = store.upload([new File(['x'], 'old.png')], oldProgress);
    store.cancel();
    const newFile = new File(['y'], 'new.bin');
    const newUpload = store.upload([newFile]);
    const newSignal = api.uploadAttachment.mock.calls[0]![4] as AbortSignal;

    thumbnail.resolve(new Blob(['thumbnail']));
    expect(await oldUpload).toEqual([]);
    expect(api.uploadAttachment).toHaveBeenCalledTimes(1);
    expect(api.uploadAttachment.mock.calls[0]![2]).toBe(newFile);
    expect(store.busy).toBe(true);
    expect(store.activeUploads).toHaveLength(1);
    expect(newSignal.aborted).toBe(false);
    expect(oldProgress).not.toHaveBeenCalled();

    newResponse.resolve(ok(attachment));
    expect(await newUpload).toEqual([attachment]);
    expect(store.busy).toBe(false);
  });

  it('settles cancelled batches with their completed files and ignores late progress and responses', async () => {
    const pending = deferred<ReturnType<typeof ok<CardAttachment>>>();
    api.uploadAttachment.mockResolvedValueOnce(ok(attachment)).mockReturnValueOnce(pending.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    const firstProgress = vi.fn();
    const first = store.upload([new File(['a'], 'done.bin'), new File(['b'], 'pending.bin')], firstProgress);
    const second = store.upload([new File(['c'], 'queued.bin')]);
    await vi.waitFor(() => expect(api.uploadAttachment).toHaveBeenCalledTimes(2));
    const callback = api.uploadAttachment.mock.calls[1]![3] as (percent: number) => void;
    const signal = api.uploadAttachment.mock.calls[1]![4] as AbortSignal;

    store.cancel();

    expect(await first).toEqual([attachment]);
    expect(await second).toEqual([]);
    expect(signal.aborted).toBe(true);
    callback(99);
    pending.resolve(ok({ ...attachment, id: 2 }));
    await Promise.resolve();
    expect(firstProgress).not.toHaveBeenCalled();
    expect(store.items).toEqual([attachment]);
    expect(api.uploadAttachment).toHaveBeenCalledTimes(2);
    expect(store.activeUploads).toEqual([]);
    expect(store.busy).toBe(false);
  });

  it('does not let a cancelled final refresh complete or clear a new queue', async () => {
    const refresh = deferred<ReturnType<typeof listing>>();
    const newResponse = deferred<ReturnType<typeof ok<CardAttachment>>>();
    api.uploadAttachment.mockResolvedValueOnce(ok(attachment)).mockReturnValueOnce(newResponse.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    api.getAttachments.mockReturnValueOnce(refresh.promise);
    const oldUpload = store.upload([new File(['a'], 'old.bin')]);
    await vi.waitFor(() => expect(api.getAttachments).toHaveBeenCalledTimes(2));
    store.clear();
    await store.open(2, 2);
    const newUpload = store.upload([new File(['b'], 'new.bin')]);

    refresh.resolve(listing([attachment]));
    expect(await oldUpload).toEqual([attachment]);
    expect(store.items).toEqual([]);
    expect(store.busy).toBe(true);
    expect(store.activeUploads).toHaveLength(1);
    newResponse.resolve(ok({ ...attachment, id: 2 }));
    expect(await newUpload).toEqual([{ ...attachment, id: 2 }]);
    expect(store.busy).toBe(false);
  });

  it('releases queued callers and busy state if an upload unexpectedly rejects', async () => {
    const pending = deferred<ReturnType<typeof ok<CardAttachment>>>();
    api.uploadAttachment.mockReturnValueOnce(pending.promise);
    const store = useAttachmentStore();
    await store.open(1, 1);
    const first = store.upload([new File(['a'], 'first.bin')]);
    const second = store.upload([new File(['b'], 'second.bin')]);
    const firstRejected = expect(first).rejects.toThrow('Unexpected upload failure');
    const secondRejected = expect(second).rejects.toThrow('Unexpected upload failure');

    pending.reject(new Error('Unexpected upload failure'));

    await Promise.all([firstRejected, secondRejected]);
    expect(store.busy).toBe(false);
    expect(store.activeUploads).toEqual([]);
    api.uploadAttachment.mockResolvedValueOnce(ok(attachment));
    expect(await store.upload([new File(['c'], 'next.bin')])).toEqual([attachment]);
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
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 1, attachmentId: image.id, originalFileName: image.originalFileName, hasThumbnail: false }
    ]));

    await store.upload([new File(['abc'], image.originalFileName, { type: 'image/png' })]);

    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(1, [1]);
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
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}
