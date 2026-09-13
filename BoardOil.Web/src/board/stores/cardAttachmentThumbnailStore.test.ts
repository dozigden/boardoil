import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { err, ok } from '../../shared/types/result';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import type { AppError } from '../../shared/types/appError';
import type { CardAttachmentImageCandidate } from '../../shared/types/attachmentTypes';
import type { Result } from '../../shared/types/result';

const api = {
  supportsAttachments: true,
  getFirstAttachmentImagesByCard: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('cardAttachmentThumbnailStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.supportsAttachments = true;
    api.getFirstAttachmentImagesByCard.mockResolvedValue(ok([]));
  });

  it('loads one batched candidate projection for an enabled board', async () => {
    api.getFirstAttachmentImagesByCard.mockResolvedValue(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();

    await store.loadBoard(4, true);

    expect(api.getFirstAttachmentImagesByCard).toHaveBeenCalledWith(4);
    expect(store.getForCard(2)).toEqual({
      cardId: 2,
      attachmentId: 8,
      originalFileName: 'cover.png',
      hasThumbnail: true
    });
  });

  it('clears candidates without making a request when disabled', async () => {
    api.getFirstAttachmentImagesByCard.mockResolvedValueOnce(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    vi.clearAllMocks();

    await store.loadBoard(4, false);

    expect(api.getFirstAttachmentImagesByCard).not.toHaveBeenCalled();
    expect(store.getForCard(2)).toBeNull();
  });

  it('leaves the board usable when the candidate projection fails', async () => {
    api.getFirstAttachmentImagesByCard.mockResolvedValue(err({ kind: 'api', message: 'Unavailable' }));
    const store = useCardAttachmentThumbnailStore();

    await store.loadBoard(4, true);

    expect(store.candidatesByCardId).toEqual({});
  });

  it('records a successful backfill only for the matching candidate', async () => {
    api.getFirstAttachmentImagesByCard.mockResolvedValue(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: false }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    store.markHasThumbnail(2, 9);
    expect(store.getForCard(2)?.hasThumbnail).toBe(false);

    store.markHasThumbnail(2, 8);
    expect(store.getForCard(2)?.hasThumbnail).toBe(true);
  });

  it('uses the first supported image attachment added to a card', async () => {
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    store.attachmentAdded(4, 2, attachment(8, 'notes.txt'));
    store.attachmentAdded(4, 2, attachment(9, 'first.png', true));
    store.attachmentAdded(4, 2, attachment(10, 'second.jpg', true));

    expect(store.getForCard(2)).toEqual({
      cardId: 2,
      attachmentId: 9,
      originalFileName: 'first.png',
      hasThumbnail: true
    });
  });

  it('refreshes the affected card to select a fallback after its first image is deleted', async () => {
    api.getFirstAttachmentImagesByCard
      .mockResolvedValueOnce(ok([
        { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true }
      ]))
      .mockResolvedValueOnce(ok([
        { cardId: 2, attachmentId: 9, originalFileName: 'second.png', hasThumbnail: false }
      ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    await store.attachmentDeleted(4, 2, 8);

    expect(api.getFirstAttachmentImagesByCard).toHaveBeenLastCalledWith(4, [2]);
    expect(store.getForCard(2)).toEqual({
      cardId: 2,
      attachmentId: 9,
      originalFileName: 'second.png',
      hasThumbnail: false
    });
  });

  it('does not refresh when an attachment other than the displayed candidate is deleted', async () => {
    api.getFirstAttachmentImagesByCard.mockResolvedValueOnce(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    vi.clearAllMocks();

    await store.attachmentDeleted(4, 2, 9);

    expect(api.getFirstAttachmentImagesByCard).not.toHaveBeenCalled();
    expect(store.getForCard(2)?.attachmentId).toBe(8);
  });

  it('suppresses targeted refreshes while thumbnails are disabled', async () => {
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, false);

    await store.refreshCards(4, [2]);
    store.attachmentAdded(4, 2, attachment(8, 'cover.png', true));

    expect(api.getFirstAttachmentImagesByCard).not.toHaveBeenCalled();
    expect(store.getForCard(2)).toBeNull();
  });

  it('lets an authoritative refresh replace an image added while that refresh was in flight', async () => {
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    const pending = deferred<Result<CardAttachmentImageCandidate[], AppError>>();
    api.getFirstAttachmentImagesByCard.mockReturnValueOnce(pending.promise);

    const refresh = store.refreshCards(4, [2]);
    store.attachmentAdded(4, 2, attachment(9, 'new.png', true));
    pending.resolve(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'copied.png', hasThumbnail: true }
    ]));
    await refresh;

    expect(store.getForCard(2)?.attachmentId).toBe(8);
  });
});

function attachment(id: number, originalFileName: string, hasThumbnail = false) {
  return {
    id,
    originalFileName,
    contentType: 'application/octet-stream',
    byteLength: 10,
    createdAtUtc: `2026-01-01T00:00:${String(id).padStart(2, '0')}Z`,
    createdByUserId: 1,
    hasThumbnail
  };
}

function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  const promise = new Promise<T>(promiseResolve => {
    resolve = promiseResolve;
  });
  return { promise, resolve };
}
