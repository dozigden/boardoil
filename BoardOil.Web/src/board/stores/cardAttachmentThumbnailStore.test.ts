import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { err, ok } from '../../shared/types/result';
import { useCardAttachmentThumbnailStore } from './cardAttachmentThumbnailStore';

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
});
