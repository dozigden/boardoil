import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCommentStore } from './commentStore';
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

    const oldBoardLoad = store.loadCardComments(1, 7);
    await store.loadCardComments(2, 7);
    oldBoardResponse.resolve(ok([makeComment(1, 7, 'Stale board one comment')]));
    await oldBoardLoad;

    expect(store.getCommentsForCard(7)).toEqual([boardTwoComment]);
  });

  it('orders comments by their semantic posting time', async () => {
    const store = useCommentStore();
    const postedLater = makeComment(1, 7, 'Posted later');
    const createdLater = {
      ...makeComment(2, 7, 'Created later'),
      postedAtUtc: '2026-07-31T11:00:00Z',
      createdAtUtc: '2026-07-31T13:00:00Z'
    };
    api.getCardComments.mockResolvedValue(ok([createdLater, postedLater]));

    await store.loadCardComments(1, 7);

    expect(store.getCommentsForCard(7).map(comment => comment.text))
      .toEqual(['Posted later', 'Created later']);
  });
});

function makeComment(id: number, cardId: number, text: string): CardComment {
  return {
    id,
    cardId,
    authorUserId: null,
    text,
    postedAtUtc: `2026-07-31T12:0${id}:00Z`,
    createdAtUtc: `2026-07-31T12:0${id}:00Z`
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
