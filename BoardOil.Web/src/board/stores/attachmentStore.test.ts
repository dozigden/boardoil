import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAttachmentStore } from './attachmentStore';
import { err, ok } from '../../shared/types/result';
import type { CardAttachment } from '../../shared/types/attachmentTypes';

const api = { supportsAttachments: true, getAttachments: vi.fn(), uploadAttachment: vi.fn(), deleteAttachment: vi.fn(), downloadAttachment: vi.fn() };
vi.mock('../../shared/api/boardApi', () => ({ createBoardApi: () => api }));
const attachment: CardAttachment = { id: 1, originalFileName: 'file.bin', byteLength: 3,
  contentType: 'application/octet-stream', createdAtUtc: '2026-09-11T12:00:00Z', createdByUserId: null };
const listing = (items: CardAttachment[] = []) => ok({ items, maxUploadByteLength: 10 });

describe('attachments', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
    api.supportsAttachments = true;
    api.getAttachments.mockResolvedValue(listing());
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
