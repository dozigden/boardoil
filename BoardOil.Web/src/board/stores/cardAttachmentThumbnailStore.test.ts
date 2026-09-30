import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { err, ok } from '../../shared/types/result';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';
import type { AppError } from '../../shared/types/appError';
import type { CardAttachmentImageCandidate } from '../../shared/types/attachmentTypes';
import type { Result } from '../../shared/types/result';

const api = {
  supportsAttachments: true,
  getCardThumbnails: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('cardAttachmentThumbnailStore', () => {
  afterEach(() => {
    useCardAttachmentThumbnailStore().clear();
    vi.restoreAllMocks();
  });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.supportsAttachments = true;
    api.getCardThumbnails.mockResolvedValue(ok([]));
  });

  it('loads one batched candidate projection for an enabled board', async () => {
    api.getCardThumbnails.mockResolvedValue(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();

    await store.loadBoard(4, true);

    expect(api.getCardThumbnails).toHaveBeenCalledWith(4);
    expect(store.getForCard(2)).toEqual({
      cardId: 2,
      attachmentId: 8,
      originalFileName: 'cover.png',
      hasThumbnail: true
    });
  });

  it('clears candidates without making a request when disabled', async () => {
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    vi.clearAllMocks();

    await store.loadBoard(4, false);

    expect(api.getCardThumbnails).not.toHaveBeenCalled();
    expect(store.getForCard(2)).toBeNull();
  });

  it('leaves the board usable when the candidate projection fails', async () => {
    api.getCardThumbnails.mockResolvedValue(err({ kind: 'api', message: 'Unavailable' }));
    const store = useCardAttachmentThumbnailStore();

    await store.loadBoard(4, true);

    expect(store.candidatesByCardId).toEqual({});
  });

  it('records a successful backfill only for the matching candidate', async () => {
    api.getCardThumbnails.mockResolvedValue(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'cover.png', hasThumbnail: false }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    store.markHasThumbnail(2, 9);
    expect(store.getForCard(2)?.hasThumbnail).toBe(false);

    store.markHasThumbnail(2, 8);
    expect(store.getForCard(2)?.hasThumbnail).toBe(true);
  });

  it('retains downloaded thumbnails across refreshes of the same board', async () => {
    const store = useCardAttachmentThumbnailStore();
    const image = new Blob(['thumbnail'], { type: 'image/png' });
    await store.loadBoard(4, true);
    const imageUrl = store.cacheThumbnailImage(4, 8, image);

    await store.loadBoard(4, true);

    expect(imageUrl).toEqual(expect.any(String));
    expect(store.getThumbnailImageUrl(4, 8)).toBe(imageUrl);
    expect(store.cacheThumbnailImage(4, 8, image)).toBe(imageUrl);
    expect(store.getThumbnailImageUrl(5, 8)).toBeNull();
  });

  it('clears downloaded thumbnails on board changes and disposal', async () => {
    const revokeUrl = vi.spyOn(URL, 'revokeObjectURL');
    const store = useCardAttachmentThumbnailStore();
    const image = new Blob(['thumbnail'], { type: 'image/png' });
    await store.loadBoard(4, true);
    const firstUrl = store.cacheThumbnailImage(4, 8, image);

    await store.loadBoard(5, true);
    store.cacheThumbnailImage(4, 8, image);
    expect(revokeUrl).toHaveBeenCalledWith(firstUrl);
    expect(store.getThumbnailImageUrl(5, 8)).toBeNull();

    await store.loadBoard(4, true);
    expect(store.getThumbnailImageUrl(4, 8)).toBeNull();
    const secondUrl = store.cacheThumbnailImage(4, 8, image);
    store.clear();
    expect(revokeUrl).toHaveBeenCalledWith(secondUrl);
    store.cacheThumbnailImage(4, 8, image);
    await store.loadBoard(4, true);
    expect(store.getThumbnailImageUrl(4, 8)).toBeNull();
  });

  it('discards a deleted attachment thumbnail while retaining other images', async () => {
    const revokeUrl = vi.spyOn(URL, 'revokeObjectURL');
    const store = useCardAttachmentThumbnailStore();
    const image = new Blob(['thumbnail'], { type: 'image/png' });
    await store.loadBoard(4, true);
    const deletedUrl = store.cacheThumbnailImage(4, 8, image);
    const retainedUrl = store.cacheThumbnailImage(4, 9, image);

    await store.attachmentDeleted(4, 2, 8);

    expect(store.getThumbnailImageUrl(4, 8)).toBeNull();
    expect(store.getThumbnailImageUrl(4, 9)).toBe(retainedUrl);
    expect(revokeUrl).toHaveBeenCalledWith(deletedUrl);
    expect(revokeUrl).not.toHaveBeenCalledWith(retainedUrl);
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
    api.getCardThumbnails
      .mockResolvedValueOnce(ok([
        { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true }
      ]))
      .mockResolvedValueOnce(ok([
        { cardId: 2, attachmentId: 9, originalFileName: 'second.png', hasThumbnail: false }
      ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    await store.attachmentDeleted(4, 2, 8);

    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(4, [2]);
    expect(store.getForCard(2)).toEqual({
      cardId: 2,
      attachmentId: 9,
      originalFileName: 'second.png',
      hasThumbnail: false
    });
  });

  it('refreshes when an attachment other than the displayed candidate is deleted', async () => {
    api.getCardThumbnails.mockResolvedValue(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    vi.clearAllMocks();

    await store.attachmentDeleted(4, 2, 9);

    expect(api.getCardThumbnails).toHaveBeenCalledWith(4, [2]);
    expect(store.getForCard(2)?.attachmentId).toBe(8);
  });

  it('refreshes a second deletion while the first fallback is still pending', async () => {
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    const first = deferred<Result<CardAttachmentImageCandidate[], AppError>>();
    const second = deferred<Result<CardAttachmentImageCandidate[], AppError>>();
    api.getCardThumbnails.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);

    const firstDeletion = store.attachmentDeleted(4, 2, 8);
    expect(store.getForCard(2)).toBeNull();
    const secondDeletion = store.attachmentDeleted(4, 2, 9);

    expect(api.getCardThumbnails).toHaveBeenCalledTimes(3);
    expect(api.getCardThumbnails).toHaveBeenLastCalledWith(4, [2]);
    first.resolve(ok([
      { cardId: 2, attachmentId: 9, originalFileName: 'second.png', hasThumbnail: true }
    ]));
    await firstDeletion;
    second.resolve(ok([
      { cardId: 2, attachmentId: 10, originalFileName: 'third.png', hasThumbnail: true }
    ]));
    await secondDeletion;

    expect(store.getForCard(2)?.attachmentId).toBe(10);
  });

  it('clears a card with no remaining image without changing other cards', async () => {
    api.getCardThumbnails.mockResolvedValueOnce(ok([
      { cardId: 2, attachmentId: 8, originalFileName: 'first.png', hasThumbnail: true },
      { cardId: 3, attachmentId: 9, originalFileName: 'other.png', hasThumbnail: true }
    ]));
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);

    await store.attachmentDeleted(4, 2, 8);

    expect(store.getForCard(2)).toBeNull();
    expect(store.getForCard(3)?.attachmentId).toBe(9);
  });

  describe.each(['load', 'refresh'] as const)('pending %s responses', operation => {
    it.each(['board change', 'clear', 'disable'] as const)('ignores a response after %s', async change => {
      const store = useCardAttachmentThumbnailStore();
      await store.loadBoard(4, true);
      const pending = deferred<Result<CardAttachmentImageCandidate[], AppError>>();
      api.getCardThumbnails.mockReturnValueOnce(pending.promise);
      const request = operation === 'load' ? store.loadBoard(4, true) : store.refreshCards(4, [2]);

      switch (change) {
        case 'board change':
          api.getCardThumbnails.mockResolvedValueOnce(ok([
            { cardId: 2, attachmentId: 10, originalFileName: 'other-board.png', hasThumbnail: true }
          ]));
          await store.loadBoard(5, true);
          break;
        case 'clear':
          store.clear();
          break;
        case 'disable':
          await store.loadBoard(4, false);
          break;
      }

      pending.resolve(ok([
        { cardId: 2, attachmentId: 8, originalFileName: 'old.png', hasThumbnail: true }
      ]));
      await request;

      if (change === 'board change') {
        expect(store.getForCard(2)?.attachmentId).toBe(10);
      } else {
        expect(store.getForCard(2)).toBeNull();
      }
    });
  });

  it('suppresses targeted refreshes while thumbnails are disabled', async () => {
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, false);

    await store.refreshCards(4, [2]);
    store.attachmentAdded(4, 2, attachment(8, 'cover.png', true));

    expect(api.getCardThumbnails).not.toHaveBeenCalled();
    expect(store.getForCard(2)).toBeNull();
  });

  it('lets an authoritative refresh replace an image added while that refresh was in flight', async () => {
    const store = useCardAttachmentThumbnailStore();
    await store.loadBoard(4, true);
    const pending = deferred<Result<CardAttachmentImageCandidate[], AppError>>();
    api.getCardThumbnails.mockReturnValueOnce(pending.promise);

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
