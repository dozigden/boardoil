import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCardTypeStore } from './cardTypeStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

const api = {
  getCardTypes: vi.fn(),
  createCardType: vi.fn(),
  updateCardType: vi.fn(),
  deleteCardType: vi.fn(),
  setDefaultCardType: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('cardTypeStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getCardTypes.mockResolvedValue(ok([]));
    api.createCardType.mockResolvedValue(ok(makeCardType(10, 'Story')));
    api.updateCardType.mockResolvedValue(ok(makeCardType(10, 'Story Updated')));
    api.deleteCardType.mockResolvedValue(ok(undefined));
    api.setDefaultCardType.mockResolvedValue(ok(undefined));
  });

  it('loads card types for the selected board', async () => {
    const store = useCardTypeStore();
    api.getCardTypes.mockResolvedValueOnce(ok([makeCardType(10, 'Story')]));

    const loaded = await store.loadCardTypes(3);

    expect(loaded).toBe(true);
    expect(api.getCardTypes).toHaveBeenCalledWith(3);
    expect(store.cardTypes.map(x => x.name)).toEqual(['Story']);
  });

  describe.each(['board change', 'disposal'] as const)('pending loads after %s', change => {
    it.each(['success', 'failure'] as const)('ignores late %s without changing feedback', async outcome => {
      const store = useCardTypeStore();
      const feedback = useUiFeedbackStore();
      const pending = createDeferred<Result<ReturnType<typeof makeCardType>[], AppError>>();
      api.getCardTypes.mockReturnValueOnce(pending.promise);
      const load = store.loadCardTypes(1);

      if (change === 'board change') {
        api.getCardTypes.mockResolvedValueOnce(ok([makeCardType(20, 'Second Board Type')]));
        await store.loadCardTypes(2);
      } else {
        store.dispose();
      }
      feedback.setError('Current feedback', 'cardType');

      if (outcome === 'success') {
        pending.resolve(ok([makeCardType(10, 'First Board Type')]));
      } else {
        pending.resolve(err({ kind: 'api', message: 'Old board load failed' }));
      }

      expect(await load).toBe(false);
      expect(feedback.errorMessage).toBe('Current feedback');
      if (change === 'board change') {
        expect(store.activeBoardId).toBe(2);
        expect(store.cardTypes.map(x => x.name)).toEqual(['Second Board Type']);
      } else {
        expect(store.activeBoardId).toBeNull();
        expect(store.cardTypes).toEqual([]);
      }
    });
  });

  it('reports errors from API operations', async () => {
    const store = useCardTypeStore();
    const feedback = useUiFeedbackStore();
    api.getCardTypes.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load card types.' }));

    const loaded = await store.loadCardTypes(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load card types.');
  });

  describe.each(['create', 'update', 'delete', 'default'] as const)('%s completion', operation => {
    it.each(['board change', 'disposal'] as const)('preserves current feedback and busy state after %s', async transition => {
      const store = useCardTypeStore();
      const feedback = useUiFeedbackStore();
      await store.loadCardTypes(1);
      const success = createDeferred<Result<ReturnType<typeof makeCardType> | undefined, AppError>>();
      const failure = createDeferred<Result<never, AppError>>();
      const model = { name: 'Story', emoji: null, styleName: 'auto' as const, stylePropertiesJson: '{}' };
      let requests: Promise<unknown>[];
      switch (operation) {
        case 'create':
          api.createCardType.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
          requests = [store.createCardType(model, 1), store.createCardType(model, 1)];
          break;
        case 'update':
          api.updateCardType.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
          requests = [store.updateCardType(10, model, 1), store.updateCardType(10, model, 1)];
          break;
        case 'delete':
          api.deleteCardType.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
          requests = [store.deleteCardType(10, 1), store.deleteCardType(10, 1)];
          break;
        case 'default':
          api.setDefaultCardType.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
          requests = [store.setDefaultCardType(10, 1), store.setDefaultCardType(10, 1)];
          break;
      }

      let currentRequest: Promise<unknown> | undefined;
      const current = createDeferred<Result<ReturnType<typeof makeCardType>, AppError>>();
      if (transition === 'board change') {
        await store.loadCardTypes(2);
        api.createCardType.mockReturnValueOnce(current.promise);
        currentRequest = store.createCardType(model, 2);
      } else {
        store.dispose();
      }
      feedback.setError('Current feedback', 'cardType');
      const loadCount = api.getCardTypes.mock.calls.length;
      if (operation === 'create' || operation === 'update') {
        success.resolve(ok(makeCardType(10, 'Old board type')));
      } else {
        success.resolve(ok(undefined));
      }
      await requests[0];
      expect(feedback.errorMessage).toBe('Current feedback');
      expect(store.busy).toBe(transition === 'board change');
      failure.resolve(err({ kind: 'api', message: 'Old failure' }));
      await requests[1];
      expect(feedback.errorMessage).toBe('Current feedback');
      expect(store.busy).toBe(transition === 'board change');
      expect(store.cardTypes).toEqual([]);
      expect(api.getCardTypes).toHaveBeenCalledTimes(loadCount);

      current.resolve(ok(makeCardType(20, 'Current type')));
      await currentRequest;
    });
  });
});

function makeCardType(id: number, name: string) {
  return {
    id,
    name,
    styleName: 'auto' as const,
    stylePropertiesJson: '{}',
    emoji: null,
    isSystem: false,
    createdAtUtc: '2026-05-16T00:00:00Z',
    updatedAtUtc: '2026-05-16T00:00:00Z'
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}
