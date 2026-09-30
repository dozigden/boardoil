import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useSlickStore } from './slickStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';
import type { AppError } from '../../shared/types/appError';
import type { Slick } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';

const api = {
  getSlicks: vi.fn(),
  getSlickCreateDefaultStyle: vi.fn(),
  createSlick: vi.fn(),
  updateSlick: vi.fn(),
  deleteSlick: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('slickStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getSlicks.mockResolvedValue(ok([]));
    api.getSlickCreateDefaultStyle.mockResolvedValue(ok({
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":5,"textColorMode":"auto"}'
    }));
    api.createSlick.mockResolvedValue(ok(makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')));
    api.updateSlick.mockResolvedValue(ok(makeSlick(7, 'Release Train', 'solid', '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}')));
    api.deleteSlick.mockResolvedValue(ok(undefined));
  });

  it('loads slicks for the selected board', async () => {
    const store = useSlickStore();
    api.getSlicks.mockResolvedValueOnce(ok([makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')]));

    const loaded = await store.loadSlicks(3);

    expect(loaded).toBe(true);
    expect(api.getSlicks).toHaveBeenCalledWith(3);
    expect(store.slicks.map(x => x.name)).toEqual(['Release Train']);
  });

  describe.each(['board change', 'disposal'] as const)('pending loads after %s', change => {
    it.each(['success', 'failure'] as const)('ignores late %s without changing feedback', async outcome => {
      const store = useSlickStore();
      const feedback = useUiFeedbackStore();
      const pending = createDeferred<Result<Slick[], AppError>>();
      api.getSlicks.mockReturnValueOnce(pending.promise);
      const load = store.loadSlicks(1);

      if (change === 'board change') {
        api.getSlicks.mockResolvedValueOnce(ok([makeSlick(20, 'Second Board Slick', 'presets', '{"presetIndex":1}')]));
        await store.loadSlicks(2);
      } else {
        store.dispose();
      }
      feedback.setError('Current feedback');

      if (outcome === 'success') {
        pending.resolve(ok([makeSlick(10, 'First Board Slick', 'presets', '{"presetIndex":2}')]));
      } else {
        pending.resolve(err({ kind: 'api', message: 'Old board load failed' }));
      }

      expect(await load).toBe(false);
      expect(feedback.errorMessage).toBe('Current feedback');
      if (change === 'board change') {
        expect(store.activeBoardId).toBe(2);
        expect(store.slicks.map(x => x.name)).toEqual(['Second Board Slick']);
      } else {
        expect(store.activeBoardId).toBeNull();
        expect(store.slicks).toEqual([]);
      }
    });
  });

  describe.each(['create', 'update', 'delete'] as const)('pending %s', operation => {
    it.each(['board change', 'disposal'] as const)('does not alter the catalogue after %s', async change => {
      const store = useSlickStore();
      await store.loadSlicks(1);
      const saved = makeSlick(7, 'Old board slick', 'presets', '{"presetIndex":2}');
      const pending = createDeferred<Result<Slick | void, AppError>>();
      let write: Promise<Slick | boolean | null>;
      switch (operation) {
        case 'create':
          api.createSlick.mockReturnValueOnce(pending.promise);
          write = store.createSlick(saved, 1);
          break;
        case 'update':
          api.updateSlick.mockReturnValueOnce(pending.promise);
          write = store.updateSlick(saved.id, saved, 1);
          break;
        case 'delete':
          api.deleteSlick.mockReturnValueOnce(pending.promise);
          write = store.deleteSlick(saved.id, 1);
          break;
      }

      const current = makeSlick(7, 'Current board slick', 'presets', '{"presetIndex":3}');
      if (change === 'board change') {
        api.getSlicks.mockResolvedValueOnce(ok([current]));
        await store.loadSlicks(2);
      } else {
        store.dispose();
      }

      if (operation === 'delete') {
        pending.resolve(ok(undefined));
        expect(await write).toBe(true);
      } else {
        pending.resolve(ok(saved));
        expect(await write).toEqual(saved);
      }
      expect(store.slicks).toEqual(change === 'board change' ? [current] : []);
      expect(api.getSlicks).toHaveBeenCalledTimes(change === 'board change' ? 2 : 1);
    });
  });

  it('creates and caches slick', async () => {
    const store = useSlickStore();
    store.activeBoardId = 3;
    const created = await store.createSlick({
      name: 'Release Train',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":2}'
    }, 3);

    expect(created?.id).toBe(7);
    expect(api.createSlick).toHaveBeenCalledWith(3, {
      name: 'Release Train',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":2}'
    });
    expect(store.slicks.map(x => x.name)).toEqual(['Release Train']);
  });

  it('upserts slicks immediately without a catalogue request', () => {
    const store = useSlickStore();
    store.activeBoardId = 1;
    const slick = makeSlick(7, 'New slick', 'presets', '{"presetIndex":2}');

    store.upsertSlick(1, slick);
    store.upsertSlick(1, slick);

    expect(store.slicks).toEqual([slick]);
    expect(api.getSlicks).not.toHaveBeenCalled();
  });

  it('replaces an existing slick by id and reorders it after a rename', () => {
    const store = useSlickStore();
    store.activeBoardId = 1;
    const current = makeSlick(7, 'Alpha', 'presets', '{"presetIndex":2}');
    const other = makeSlick(8, 'Middle', 'presets', '{"presetIndex":3}');
    const updated = makeSlick(7, 'Zulu', 'solid', '{"backgroundColor":"#336699"}');
    store.slicks = [current, other];

    store.upsertSlick(1, updated);

    expect(store.slicks).toEqual([other, updated]);
  });

  it('ignores embedded slicks from another board or a disposed context', () => {
    const store = useSlickStore();
    store.activeBoardId = 2;
    const slick = makeSlick(7, 'Old board slick', 'presets', '{"presetIndex":2}');

    store.upsertSlick(1, slick);
    expect(store.slicks).toEqual([]);

    store.dispose();
    store.upsertSlick(2, slick);
    expect(store.slicks).toEqual([]);
    expect(store.activeBoardId).toBeNull();
    expect(api.getSlicks).not.toHaveBeenCalled();
  });

  it('loads create default style', async () => {
    const store = useSlickStore();

    const defaultStyle = await store.getCreateDefaultStyle(3);

    expect(defaultStyle?.stylePropertiesJson).toBe('{"presetIndex":5,"textColorMode":"auto"}');
    expect(api.getSlickCreateDefaultStyle).toHaveBeenCalledWith(3);
  });

  it('updates slick in cache', async () => {
    const store = useSlickStore();
    store.slicks = [makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')];
    store.activeBoardId = 3;
    const updated = await store.updateSlick(7, {
      name: 'Release Train',
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}'
    }, 3);

    expect(updated?.styleName).toBe('solid');
    expect(store.slicks[0]?.styleName).toBe('solid');
  });

  it('deletes slick from cache', async () => {
    const store = useSlickStore();
    store.slicks = [makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')];
    store.activeBoardId = 3;
    const deleted = await store.deleteSlick(7, 3);

    expect(deleted).toBe(true);
    expect(api.deleteSlick).toHaveBeenCalledWith(3, 7);
    expect(store.slicks).toHaveLength(0);
  });

  it('reports errors from API operations', async () => {
    const store = useSlickStore();
    const feedback = useUiFeedbackStore();
    api.getSlicks.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load slicks.' }));

    const loaded = await store.loadSlicks(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load slicks.');
  });
});

function makeSlick(id: number, name: string, styleName: 'solid' | 'presets', stylePropertiesJson: string) {
  return {
    id,
    name,
    styleName,
    stylePropertiesJson,
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
