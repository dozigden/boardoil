import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCommentStore } from './commentStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AppError } from '../../shared/types/appError';
import type { CardComment } from '../../shared/types/boardTypes';
import { err, ok } from '../../shared/types/result';
import type { Result } from '../../shared/types/result';

const api = {
  getCardComments: vi.fn(),
  createCardComment: vi.fn(),
  updateCardComment: vi.fn(),
  deleteCardComment: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('commentStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it('removes a deleted comment once and retains unrelated comments', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const deleted = makeComment(1, 7, 'Deleted');
    const kept = makeComment(2, 7, 'Kept');
    store.upsertCardComment(1, deleted);
    store.upsertCardComment(1, kept);
    api.deleteCardComment.mockResolvedValueOnce(ok(undefined));

    expect(await store.deleteCardComment(1, 7, deleted.id)).toEqual(ok(undefined));
    store.removeCardComment(1, 7, deleted.id);

    expect(store.getCommentsForCard(7)).toEqual([kept]);
    expect(store.isCommentDeleted(deleted.id)).toBe(true);
  });

  it('preserves a comment on failed deletion and ignores deletion events for another board', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const comment = makeComment(1, 7, 'Kept');
    store.upsertCardComment(1, comment);
    api.deleteCardComment.mockResolvedValueOnce(err({ kind: 'api', message: 'Delete failed.' }));

    await store.deleteCardComment(1, 7, comment.id);
    store.removeCardComment(2, 7, comment.id);

    expect(store.getCommentsForCard(7)).toEqual([comment]);
    expect(store.isCommentDeleted(comment.id)).toBe(false);
    expect(useUiFeedbackStore().errorMessage).toBe('Delete failed.');
  });

  it('ignores deletion completing after the board changes', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const pending = deferred<Result<void, AppError>>();
    api.deleteCardComment.mockReturnValueOnce(pending.promise);
    const deleting = store.deleteCardComment(1, 7, 1);
    store.initialize(2);
    const current = makeComment(1, 7, 'Current board');
    store.upsertCardComment(2, current);
    pending.resolve(ok(undefined));

    expect(await deleting).toBeNull();
    expect(store.getCommentsForCard(7)).toEqual([current]);
    expect(store.isCommentDeleted(1)).toBe(false);
  });

  it('does not resurrect a deletion from delayed edits, reads or realtime events, including after resync', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const comment = makeComment(1, 7, 'Original');
    store.upsertCardComment(1, comment);
    const editResult = deferred<Result<CardComment, AppError>>();
    const loadResult = deferred<Result<CardComment[], AppError>>();
    api.updateCardComment.mockReturnValueOnce(editResult.promise);
    api.getCardComments.mockReturnValueOnce(loadResult.promise);
    const editing = store.updateCardComment(1, 7, 1, 'Late edit');
    const loading = store.loadCardComments(1, 7);
    store.removeCardComment(1, 7, 1);
    store.initialize(1);
    editResult.resolve(ok({ ...comment, text: 'Late edit' }));
    loadResult.resolve(ok([comment]));
    await editing;
    await loading;
    store.upsertCardComment(1, comment);

    expect(store.getCommentsForCard(7)).toEqual([]);
    expect(store.isCommentDeleted(1)).toBe(true);
  });

  it('recovers missed deletions on reconnect and clears deletion tracking when switching boards', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const comment = makeComment(1, 7, 'Deleted while disconnected');
    store.upsertCardComment(1, comment);
    store.initialize(1);
    api.getCardComments.mockResolvedValueOnce(ok([]));

    await store.loadCardComments(1, 7);
    store.upsertCardComment(1, comment);

    expect(store.getCommentsForCard(7)).toEqual([]);
    expect(store.isCommentDeleted(1)).toBe(true);
    store.initialize(2);
    store.upsertCardComment(2, comment);
    expect(store.getCommentsForCard(7)).toEqual([comment]);
  });

  it('replaces an edited comment without changing posting order or duplicating realtime updates', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const earlier = makeComment(1, 7, 'Earlier');
    const later = makeComment(2, 7, 'Later');
    store.upsertCardComment(1, earlier);
    store.upsertCardComment(1, later);
    const edited = { ...earlier, text: 'Edited earlier', editedAtUtc: '2026-08-01T12:00:00Z' };
    api.updateCardComment.mockResolvedValueOnce(ok(edited));

    expect(await store.updateCardComment(1, 7, earlier.id, edited.text)).toEqual(ok(edited));
    store.upsertCardComment(1, edited);

    expect(api.updateCardComment).toHaveBeenCalledWith(1, 7, earlier.id, edited.text);
    expect(store.getCommentsForCard(7)).toEqual([later, edited]);
  });

  it('keeps saved comments intact on a failed edit', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const comment = makeComment(1, 7, 'Original');
    store.upsertCardComment(1, comment);
    api.updateCardComment.mockResolvedValueOnce(err({ kind: 'api', message: 'Edit failed.' }));

    await store.updateCardComment(1, 7, comment.id, 'Replacement');

    expect(store.getCommentsForCard(7)).toEqual([comment]);
    expect(useUiFeedbackStore().errorMessage).toBe('Edit failed.');
  });

  it('ignores an edit completing after the selected board changes', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const pending = deferred<Result<CardComment, AppError>>();
    api.updateCardComment.mockReturnValueOnce(pending.promise);
    const update = store.updateCardComment(1, 7, 1, 'Old board edit');
    store.initialize(2);
    const current = makeComment(1, 7, 'Current board');
    store.upsertCardComment(2, current);
    pending.resolve(ok(makeComment(1, 7, 'Old board edit')));

    expect(await update).toBeNull();
    expect(store.getCommentsForCard(7)).toEqual([current]);
    expect(store.busy).toBe(false);
  });

  it('does not apply an old-board response over comments for the same card id on the current board', async () => {
    const store = useCommentStore();
    const oldBoardResponse = deferred<Result<CardComment[], AppError>>();
    const boardTwoComment = makeComment(2, 7, 'Board two comment');
    api.getCardComments
      .mockImplementationOnce(() => oldBoardResponse.promise)
      .mockResolvedValueOnce(ok([boardTwoComment]));

    store.initialize(1);
    const oldBoardLoad = store.loadCardComments(1, 7);
    store.initialize(2);
    await store.loadCardComments(2, 7);
    oldBoardResponse.resolve(ok([makeComment(1, 7, 'Stale board one comment')]));
    await oldBoardLoad;

    expect(store.getCommentsForCard(7)).toEqual([boardTwoComment]);
  });

  it('preserves another source\'s error after comments load successfully', async () => {
    const store = useCommentStore();
    const feedback = useUiFeedbackStore();
    store.initialize(1);
    feedback.setError('Member lookup unavailable.', 'boardMembers');
    api.getCardComments.mockResolvedValueOnce(ok([makeComment(1, 7, 'Loaded comment')]));

    await store.loadCardComments(1, 7);

    expect(store.getCommentsForCard(7)).toHaveLength(1);
    expect(feedback.errorMessage).toBe('Member lookup unavailable.');
  });

  it('clears its own load failure when a retry succeeds', async () => {
    const store = useCommentStore();
    const feedback = useUiFeedbackStore();
    store.initialize(1);
    api.getCardComments.mockResolvedValueOnce(err({ kind: 'api', message: 'Comments unavailable.' }));
    await store.loadCardComments(1, 7);
    expect(feedback.errorMessage).toBe('Comments unavailable.');
    api.getCardComments.mockResolvedValueOnce(ok([]));

    await store.loadCardComments(1, 7);

    expect(feedback.errorMessage).toBe('');
  });

  it('orders comments by their semantic posting time', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const postedLater = makeComment(1, 7, 'Posted later');
    const postedEarlier = {
      ...makeComment(2, 7, 'Posted earlier'),
      postedAtUtc: '2026-07-31T11:00:00Z'
    };
    api.getCardComments.mockResolvedValue(ok([postedEarlier, postedLater]));

    await store.loadCardComments(1, 7);

    expect(store.getCommentsForCard(7).map(comment => comment.text))
      .toEqual(['Posted later', 'Posted earlier']);
  });

  it.each(['API first', 'realtime first'])('shares comment upserts without duplicates: %s', async order => {
    const store = useCommentStore();
    store.initialize(1);
    const older = makeComment(1, 7, 'Earlier comment');
    const posted = makeComment(2, 7, 'New comment');
    store.upsertCardComment(1, older);
    const pending = deferred<Result<CardComment, AppError>>();
    api.createCardComment.mockReturnValueOnce(pending.promise);
    const post = store.addCardComment(1, 7, posted.text);

    if (order === 'realtime first') {
      store.upsertCardComment(1, posted);
    }
    pending.resolve(ok(posted));
    expect(await post).toEqual(ok(posted));
    if (order === 'API first') {
      store.upsertCardComment(1, posted);
    }

    expect(store.getCommentsForCard(7)).toEqual([posted, older]);
    store.upsertCardComment(1, { ...posted, text: 'Updated comment' });
    expect(store.getCommentsForCard(7)).toEqual([{ ...posted, text: 'Updated comment' }, older]);
  });

  it.each(['board change', 'disposal'])('ignores a pending post after %s', async change => {
    const store = useCommentStore();
    const feedback = useUiFeedbackStore();
    store.initialize(1);
    const pending = deferred<Result<CardComment, AppError>>();
    api.createCardComment.mockReturnValueOnce(pending.promise);
    const post = store.addCardComment(1, 7, 'Old board comment');
    if (change === 'board change') {
      store.initialize(2);
      store.upsertCardComment(2, makeComment(2, 7, 'Current board comment'));
    } else {
      store.dispose();
    }
    const currentComments = store.getCommentsForCard(7);
    feedback.setError('Current feedback', 'comment');

    pending.resolve(ok(makeComment(1, 7, 'Old board comment')));

    expect(await post).toBeNull();
    expect(store.getCommentsForCard(7)).toEqual(currentComments);
    expect(feedback.errorMessage).toBe('Current feedback');
  });

  it('ignores pending loads and realtime additions after disposal', async () => {
    const store = useCommentStore();
    store.initialize(1);
    const pending = deferred<Result<CardComment[], AppError>>();
    api.getCardComments.mockReturnValueOnce(pending.promise);
    const load = store.loadCardComments(1, 7);
    store.dispose();
    const comment = makeComment(1, 7, 'Old comment');

    store.upsertCardComment(1, comment);
    pending.resolve(ok([comment]));
    await load;

    expect(store.commentsByCardId).toEqual({});
  });

  it.each(['load', 'post'])('does not select a different board when starting a %s', async operation => {
    const store = useCommentStore();
    store.initialize(2);
    const current = makeComment(2, 7, 'Current board comment');
    store.upsertCardComment(2, current);
    const old = makeComment(1, 7, 'Old board comment');
    if (operation === 'load') {
      api.getCardComments.mockResolvedValueOnce(ok([old]));
      await store.loadCardComments(1, 7);
    } else {
      api.createCardComment.mockResolvedValueOnce(ok(old));
      expect(await store.addCardComment(1, 7, old.text)).toBeNull();
    }
    store.upsertCardComment(1, old);
    store.upsertCardComment(2, { ...current, text: 'Current update' });

    expect(store.getCommentsForCard(7)).toEqual([{ ...current, text: 'Current update' }]);
  });

  describe.each(['load', 'post'] as const)('%s busy state', operation => {
    it.each(['board change', 'disposal'] as const)('ignores late success and failure after %s', async transition => {
      const store = useCommentStore();
      const feedback = useUiFeedbackStore();
      store.initialize(1);
      const success = deferred<Result<CardComment | CardComment[], AppError>>();
      const failure = deferred<Result<never, AppError>>();
      let requests: Promise<unknown>[];
      if (operation === 'load') {
        api.getCardComments.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
        requests = [store.loadCardComments(1, 7), store.loadCardComments(1, 8)];
      } else {
        api.createCardComment.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
        requests = [store.addCardComment(1, 7, 'Old comment'), store.addCardComment(1, 8, 'Old comment')];
      }
      const current = deferred<Result<CardComment[], AppError>>();
      let currentRequest: Promise<unknown> | undefined;
      if (transition === 'board change') {
        store.initialize(2);
        api.getCardComments.mockReturnValueOnce(current.promise);
        currentRequest = store.loadCardComments(2, 7);
      } else {
        store.dispose();
      }
      feedback.setError('Current feedback', 'comment');
      const comment = makeComment(1, 7, 'Old comment');
      success.resolve(ok(operation === 'load' ? [comment] : comment));
      await requests[0];
      expect(store.busy).toBe(transition === 'board change');
      expect(feedback.errorMessage).toBe('Current feedback');
      failure.resolve(err({ kind: 'api', message: 'Old failure' }));
      await requests[1];
      expect(store.busy).toBe(transition === 'board change');
      expect(feedback.errorMessage).toBe('Current feedback');
      expect(store.commentsByCardId).toEqual({});
      current.resolve(ok([]));
      await currentRequest;
      expect(store.busy).toBe(false);
    });

    it('does not set busy for a request belonging to another board', async () => {
      const store = useCommentStore();
      store.initialize(2);
      const pending = deferred<Result<never, AppError>>();
      let request: Promise<unknown>;
      if (operation === 'load') {
        api.getCardComments.mockReturnValueOnce(pending.promise);
        request = store.loadCardComments(1, 7);
      } else {
        api.createCardComment.mockReturnValueOnce(pending.promise);
        request = store.addCardComment(1, 7, 'Old comment');
      }
      expect(store.busy).toBe(false);
      pending.resolve(err({ kind: 'api', message: 'Old failure' }));
      await request;
      expect(store.busy).toBe(false);
    });
  });
});

function makeComment(id: number, cardId: number, text: string): CardComment {
  return {
    id,
    cardId,
    authorUserId: null,
    isSystem: false,
    text,
    postedAtUtc: `2026-07-31T12:0${id}:00Z`
  };
}

function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}
