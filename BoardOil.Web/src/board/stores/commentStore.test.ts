import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCommentStore } from './commentStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AppError } from '../../shared/types/appError';
import type { CardComment } from '../../shared/types/boardTypes';
import { ok } from '../../shared/types/result';
import type { Result } from '../../shared/types/result';

const api = {
  getCardComments: vi.fn(),
  createCardComment: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('commentStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
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
    feedback.setError('Current feedback');

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
});

function makeComment(id: number, cardId: number, text: string): CardComment {
  return {
    id,
    cardId,
    authorUserId: null,
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
