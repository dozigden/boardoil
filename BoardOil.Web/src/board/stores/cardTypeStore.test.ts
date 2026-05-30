import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCardTypeStore } from './cardTypeStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';

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

  it('ignores stale loadCardTypes responses when board changes mid-load', async () => {
    const store = useCardTypeStore();
    const firstRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeCardType>[] }>();
    const secondRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeCardType>[] }>();

    api.getCardTypes
      .mockImplementationOnce(() => firstRequest.promise)
      .mockImplementationOnce(() => secondRequest.promise);

    const firstLoad = store.loadCardTypes(1);
    const secondLoad = store.loadCardTypes(2);

    secondRequest.resolve({ ok: true, data: [makeCardType(20, 'Second Board Type')] });
    await secondLoad;

    firstRequest.resolve({ ok: true, data: [makeCardType(10, 'First Board Type')] });
    await firstLoad;

    expect(store.activeBoardId).toBe(2);
    expect(store.cardTypes.map(x => x.name)).toEqual(['Second Board Type']);
  });

  it('reports errors from API operations', async () => {
    const store = useCardTypeStore();
    const feedback = useUiFeedbackStore();
    api.getCardTypes.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load card types.' }));

    const loaded = await store.loadCardTypes(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load card types.');
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
